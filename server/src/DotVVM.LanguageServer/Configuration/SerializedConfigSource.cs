using System.Text.Json;
using DotVVM.LanguageServer.Model;

namespace DotVVM.LanguageServer.Configuration;

/// <summary>
/// Stupeň 2: čte dotvvm_serialized_config.json.tmp, který DotVVM zapisuje
/// při startu aplikace v Debug režimu. Hledá se od zadaného adresáře nahoru.
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
            return null;      // poškozený soubor — chováme se, jako by neexistoval
        }
        catch (IOException)
        {
            return null;      // právě se přepisuje
        }
    }

    /// <summary>Hledá soubor v adresáři a pak ve všech nadřazených.</summary>
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
    /// Mapuje plný název typu na seznam jeho vlastností. Sekce properties má tvar
    /// { "Plny.Nazev.Typu": { "NazevVlastnosti": { "type": ... } } } — tedy vnořený
    /// objekt na typ, nikoli plochý klíč "Typ.Vlastnost".
    /// </summary>
    private static Dictionary<string, List<string>> ParseProperties(JsonElement root)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        if (!root.TryGetProperty("properties", out var properties) ||
            properties.ValueKind != JsonValueKind.Object)
        {
            return map;
        }

        foreach (var owner in properties.EnumerateObject())
        {
            if (owner.Value.ValueKind != JsonValueKind.Object) continue;

            var names = owner.Value.EnumerateObject().Select(p => p.Name).ToList();
            if (names.Count == 0) continue;

            map[owner.Name] = names;
        }
        return map;
    }

    private static List<ControlInfo> ParseControls(
        JsonElement root, Dictionary<string, List<string>> properties)
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
                    : Array.Empty<string>()));
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
