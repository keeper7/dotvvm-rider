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
        var controls = ParseControls(root, properties);
        return new ControlRegistry(registrations, controls);
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
    private static Dictionary<string, List<ControlProperty>> ParseProperties(JsonElement root)
    {
        var map = new Dictionary<string, List<ControlProperty>>(StringComparer.Ordinal);

        if (!root.TryGetProperty("properties", out var properties) ||
            properties.ValueKind != JsonValueKind.Object)
        {
            return map;
        }

        foreach (var owner in properties.EnumerateObject())
        {
            if (owner.Value.ValueKind != JsonValueKind.Object) continue;

            // Exclude means the property is never written in markup; drop it here so no later
            // stage has to know about it. Measured: 50 of the framework's 614.
            var parsed = owner.Value.EnumerateObject()
                .Where(p => GetString(p.Value, "mappingMode") != "Exclude")
                .Select(p => ReadProperty(p.Name, p.Value))
                .ToList();

            if (parsed.Count > 0) map[owner.Name] = parsed;
        }
        return map;
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

    private static List<ControlInfo> ParseControls(
        JsonElement root, Dictionary<string, List<ControlProperty>> properties)
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
                    : Array.Empty<ControlProperty>()));
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
