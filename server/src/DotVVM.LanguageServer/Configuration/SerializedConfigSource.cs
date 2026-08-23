using System.Text.Json;
using DotVVM.LanguageServer.Model;

namespace DotVVM.LanguageServer.Configuration;

/// <summary>
/// Tier 2: reads dotvvm_serialized_config.json.tmp, which DotVVM writes when the application
/// starts in Debug mode. The search runs from the given directory upwards.
/// </summary>
public sealed class SerializedConfigSource : IConfigurationSource
{
    public const string FileName = "dotvvm_serialized_config.json.tmp";

    public string Name => "config";

    public bool KnowsProjectPrefixes => true;

    public async Task<ControlRegistry?> LoadAsync(string projectDir, CancellationToken ct)
    {
        var file = FindConfigFile(projectDir);
        if (file is null) return null;

        try
        {
            await using var stream = File.OpenRead(file);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            return Parse(doc.RootElement);
        }
        catch (JsonException)
        {
            return null;      // a corrupt file is treated as if it did not exist
        }
        catch (IOException)
        {
            return null;      // it is being rewritten right now
        }
    }

    /// <summary>Looks for the file in the directory and then in every parent.</summary>
    private static string? FindConfigFile(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, FileName);
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return null;
    }

    private static ControlRegistry Parse(JsonElement root)
    {
        var registrations = ParseRegistrations(root);
        var properties = ParseProperties(root);
        var controls = ParseControls(root, properties.Own, ParsePropertyGroups(root));
        return new ControlRegistry(registrations, controls, properties.Attached);
    }

    private static List<ControlRegistration> ParseRegistrations(JsonElement root)
    {
        var result = new List<ControlRegistration>();

        if (!root.TryGetProperty("config", out var config) ||
            !config.TryGetProperty("markup", out var markup) ||
            !markup.TryGetProperty("controls", out var controls) ||
            controls.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var item in controls.EnumerateArray())
        {
            var prefix = GetString(item, "tagPrefix");
            if (prefix is null) continue;

            result.Add(new ControlRegistration(
                TagPrefix: prefix,
                Namespace: GetString(item, "namespace"),
                Assembly: GetString(item, "assembly"),
                TagName: GetString(item, "tagName"),
                Src: GetString(item, "src")));
        }
        return result;
    }

    /// <summary>
    /// Maps a full type name to its properties. The properties section has the shape
    /// { "Full.Type.Name": { "PropertyName": { "type": …, "mappingMode": … } } } — a nested
    /// object per type, not a flat "Type.Property" key. Metadata is written only when it differs
    /// from the default, so an absent mappingMode means Attribute.
    /// </summary>
    private static (Dictionary<string, List<ControlProperty>> Own, List<ControlProperty> Attached)
        ParseProperties(JsonElement root)
    {
        var map = new Dictionary<string, List<ControlProperty>>(StringComparer.Ordinal);
        var attached = new List<ControlProperty>();

        if (!root.TryGetProperty("properties", out var properties) ||
            properties.ValueKind != JsonValueKind.Object)
        {
            return (map, attached);
        }

        foreach (var owner in properties.EnumerateObject())
        {
            if (owner.Value.ValueKind != JsonValueKind.Object) continue;

            var own = new List<ControlProperty>();

            foreach (var property in owner.Value.EnumerateObject())
            {
                // Exclude means the property is never written in markup; drop it here so no
                // later stage has to know about it. Measured: 50 of the framework's 614.
                if (GetString(property.Value, "mappingMode") == "Exclude") continue;

                // An attached property is written as Owner.Name on any element, so it belongs
                // to no control - not even to the type that declares it.
                if (Flag(property.Value, "isAttached"))
                {
                    attached.Add(ReadProperty(
                        $"{LastSegment(owner.Name)}.{property.Name}", property.Value));
                    continue;
                }

                own.Add(ReadProperty(property.Name, property.Value));
            }

            if (own.Count > 0) map[owner.Name] = own;
        }
        return (map, attached);
    }

    private static string LastSegment(string typeName)
    {
        var withoutAssembly = typeName.Split(',')[0];
        var lastDot = withoutAssembly.LastIndexOf('.');
        return lastDot < 0 ? withoutAssembly : withoutAssembly[(lastDot + 1)..];
    }

    private static ControlProperty ReadProperty(string name, JsonElement value) =>
        new(Name: name,
            Usage: GetString(value, "mappingMode") switch
            {
                "InnerElement" => PropertyUsage.InnerElement,
                "Both" => PropertyUsage.Both,
                _ => PropertyUsage.Attribute,
            },
            Value: Flag(value, "onlyBindings") ? PropertyValue.BindingOnly
                 : Flag(value, "onlyHardcoded") ? PropertyValue.HardCodedOnly
                 : PropertyValue.Any,
            Required: Flag(value, "required"),
            TypeName: GetString(value, "type"));

    private static bool Flag(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.True;

    /// <summary>
    /// Maps a full type name to the property families it declares. The section has the shape
    /// { "Full.Type.Name": { "GroupName": { "prefix": "Class-", … } } }, with one prefix under
    /// "prefix" and several under "prefixes" — and it names only the type that declares the
    /// group, never the ones inheriting it. An empty prefix stands for "any attribute goes"
    /// and is dropped: there is nothing to offer for it.
    /// </summary>
    private static Dictionary<string, List<ControlPropertyGroup>> ParsePropertyGroups(
        JsonElement root)
    {
        var map = new Dictionary<string, List<ControlPropertyGroup>>(StringComparer.Ordinal);

        if (!root.TryGetProperty("propertyGroups", out var owners) ||
            owners.ValueKind != JsonValueKind.Object)
        {
            return map;
        }

        foreach (var owner in owners.EnumerateObject())
        {
            if (owner.Value.ValueKind != JsonValueKind.Object) continue;

            var groups = new List<ControlPropertyGroup>();

            foreach (var group in owner.Value.EnumerateObject())
            {
                var usage = GetString(group.Value, "mappingMode") switch
                {
                    "InnerElement" => PropertyUsage.InnerElement,
                    "Both" => PropertyUsage.Both,
                    "Exclude" => (PropertyUsage?)null,
                    _ => PropertyUsage.Attribute,
                };
                if (usage is null) continue;

                var value = Flag(group.Value, "onlyBindings") ? PropertyValue.BindingOnly
                          : Flag(group.Value, "onlyHardcoded") ? PropertyValue.HardCodedOnly
                          : PropertyValue.Any;
                var typeName = GetString(group.Value, "type");

                foreach (var prefix in Prefixes(group.Value))
                {
                    groups.Add(new ControlPropertyGroup(
                        prefix, group.Name, usage.Value, value, typeName));
                }
            }

            if (groups.Count > 0) map[owner.Name] = groups;
        }
        return map;
    }

    private static IEnumerable<string> Prefixes(JsonElement group)
    {
        if (GetString(group, "prefix") is { Length: > 0 } single) yield return single;

        if (!group.TryGetProperty("prefixes", out var prefixes) ||
            prefixes.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var prefix in prefixes.EnumerateArray())
        {
            if (prefix.ValueKind == JsonValueKind.String &&
                prefix.GetString() is { Length: > 0 } name)
            {
                yield return name;
            }
        }
    }

    private static List<ControlInfo> ParseControls(
        JsonElement root, Dictionary<string, List<ControlProperty>> properties,
        Dictionary<string, List<ControlPropertyGroup>> groups)
    {
        var result = new List<ControlInfo>();

        if (!root.TryGetProperty("controls", out var controls) ||
            controls.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        foreach (var entry in controls.EnumerateObject())
        {
            var typeName = entry.Name;
            result.Add(new ControlInfo(
                FullTypeName: typeName,
                BaseType: GetString(entry.Value, "baseType"),
                DefaultContentProperty: GetString(entry.Value, "defaultContentProperty"),
                Properties: properties.TryGetValue(typeName, out var props)
                    ? props
                    : Array.Empty<ControlProperty>(),
                PropertyGroups: groups.TryGetValue(typeName, out var own)
                    ? own
                    : Array.Empty<ControlPropertyGroup>()));
        }
        return result;
    }

    private static string? GetString(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
