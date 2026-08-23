using System.Diagnostics;
using System.Text.Json;
using DotVVM.LanguageServer.Model;

namespace DotVVM.LanguageServer.Configuration;

/// <summary>
/// Tier 3: runs the probe process, which loads the project's compiled assembly and returns the
/// real configuration. Available once the project has been built.
/// </summary>
public sealed class AssemblyProbeSource : IConfigurationSource
{
    /// <summary>A fixed probe path; null means picking one by the target assembly's TFM.</summary>
    private readonly string? _fixedProbePath;
    private readonly TimeSpan _timeout;

    /// <summary>Probe variants, oldest first; a newer runtime also loads an older assembly.</summary>
    private static readonly string[] ProbeFrameworks = { "net8.0", "net9.0" };

    public AssemblyProbeSource(string? probePath = null, TimeSpan? timeout = null)
    {
        _fixedProbePath = probePath;
        _timeout = timeout ?? TimeSpan.FromSeconds(30);
    }

    public string Name => "assembly";

    public bool KnowsProjectPrefixes => true;

    private static string ProbeRoot => Path.Combine(AppContext.BaseDirectory, "probe");

    public async Task<ControlRegistry?> LoadAsync(string projectDir, CancellationToken ct)
    {
        var assembly = ProjectAssembly.Find(projectDir);
        if (assembly is null) return null;

        var probe = _fixedProbePath ?? ResolveProbeFor(assembly);
        if (probe is null || !File.Exists(probe)) return null;

        var output = await RunProbeAsync(probe, assembly, projectDir, ct);
        return output is null ? null : ParseProbeOutput(output);
    }

    /// <summary>
    /// Picks the probe variant by the target assembly's TFM. The probe must run on a runtime at
    /// least as new as the target project: a net8.0 host cannot load an assembly targeting
    /// net9.0. When the TFM cannot be read, the newest available variant is used.
    /// </summary>
    private static string? ResolveProbeFor(string targetAssembly)
    {
        var wanted = ProjectAssembly.ReadTargetFramework(targetAssembly);

        var candidates = ProbeFrameworks
            .Select(tfm => (Tfm: tfm, Path: Path.Combine(ProbeRoot, tfm,
                                                         "DotVVM.LanguageServer.Probe.dll")))
            .Where(c => File.Exists(c.Path))
            .ToList();

        if (candidates.Count == 0) return null;

        if (wanted is not null)
        {
            var index = Array.IndexOf(ProbeFrameworks, wanted);
            if (index >= 0)
            {
                // The first variant that is not older than the target project
                var match = candidates.FirstOrDefault(
                    c => Array.IndexOf(ProbeFrameworks, c.Tfm) >= index);
                if (match.Path is not null) return match.Path;
            }
        }

        return candidates[^1].Path;
    }

    private async Task<string?> RunProbeAsync(
        string probePath, string assembly, string projectDir, CancellationToken ct)
    {
        var info = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        info.ArgumentList.Add(probePath);
        info.ArgumentList.Add(assembly);
        info.ArgumentList.Add(projectDir);

        using var process = Process.Start(info);
        if (process is null) return null;

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutSource.CancelAfter(_timeout);

        try
        {
            var stdout = await process.StandardOutput.ReadToEndAsync(timeoutSource.Token);
            await process.WaitForExitAsync(timeoutSource.Token);
            if (process.ExitCode != 0)
            {
                var stderr = await process.StandardError.ReadToEndAsync(ct);
                await Console.Error.WriteLineAsync($"[dotvvm-ls] probe failed: {stderr}");
                return null;
            }
            return stdout;
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* already gone */ }
            return null;
        }
    }

    /// <summary>Public so it can be tested without starting a process.</summary>
    public static ControlRegistry? ParseProbeOutput(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("Registrations", out var array) ||
                array.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var registrations = array.EnumerateArray().Select(item => new ControlRegistration(
                TagPrefix: Str(item, "TagPrefix") ?? "",
                Namespace: Str(item, "Namespace"),
                Assembly: Str(item, "Assembly"),
                TagName: Str(item, "TagName"),
                Src: Str(item, "Src"))).ToList();

            return new ControlRegistry(
                registrations,
                ParseControls(doc.RootElement),
                ReadProperties(doc.RootElement, "AttachedProperties"),
                new ProjectTypes(
                    ReadStrings(doc.RootElement, "ViewModels"),
                    ReadStrings(doc.RootElement, "Namespaces")));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Reads an array of plain strings. A probe that predates the key omits it, and that must
    /// not cost the rest of the tier - the same tolerance the control types get.
    /// </summary>
    private static List<string> ReadStrings(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var array) || array.ValueKind != JsonValueKind.Array)
            return new List<string>();

        return array.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .OfType<string>()
            .ToList();
    }

    /// <summary>
    /// Reads the control types. A probe that predates them omits the key altogether, and that
    /// must not cost the registrations: an outdated bundled probe would take the whole tier down.
    /// </summary>
    private static List<ControlInfo> ParseControls(JsonElement root)
    {
        var result = new List<ControlInfo>();

        if (!root.TryGetProperty("Controls", out var array) ||
            array.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var item in array.EnumerateArray())
        {
            var fullTypeName = Str(item, "FullTypeName");
            if (fullTypeName is null) continue;

            result.Add(new ControlInfo(
                FullTypeName: fullTypeName,
                BaseType: Str(item, "BaseType"),
                DefaultContentProperty: Str(item, "DefaultContentProperty"),
                Properties: ReadProperties(item, "Properties"),
                PropertyGroups: ReadGroups(item)));
        }

        return result;
    }

    /// <summary>
    /// Reads the property families. Absent from an older probe, which must cost nothing but the
    /// groups themselves.
    /// </summary>
    private static List<ControlPropertyGroup> ReadGroups(JsonElement element)
    {
        var result = new List<ControlPropertyGroup>();

        if (!element.TryGetProperty("PropertyGroups", out var array) ||
            array.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var item in array.EnumerateArray())
        {
            var prefix = Str(item, "Prefix");
            if (string.IsNullOrEmpty(prefix)) continue;

            result.Add(new ControlPropertyGroup(
                Prefix: prefix,
                Name: Str(item, "Name") ?? prefix,
                Usage: Enum.TryParse<PropertyUsage>(Str(item, "Usage"), out var usage)
                    ? usage : PropertyUsage.Attribute,
                Value: Enum.TryParse<PropertyValue>(Str(item, "Value"), out var value)
                    ? value : PropertyValue.Any,
                TypeName: Str(item, "TypeName")));
        }
        return result;
    }

    private static List<ControlProperty> ReadProperties(JsonElement element, string key)
    {
        var result = new List<ControlProperty>();

        if (!element.TryGetProperty(key, out var array) ||
            array.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var item in array.EnumerateArray())
        {
            var name = Str(item, "Name");
            if (name is null) continue;

            result.Add(new ControlProperty(
                Name: name,
                Usage: Enum.TryParse<PropertyUsage>(Str(item, "Usage"), out var usage)
                    ? usage : PropertyUsage.Attribute,
                Value: Enum.TryParse<PropertyValue>(Str(item, "Value"), out var value)
                    ? value : PropertyValue.Any,
                Required: item.TryGetProperty("Required", out var required) &&
                          required.ValueKind == JsonValueKind.True,
                TypeName: Str(item, "TypeName")));
        }
        return result;
    }

    private static string? Str(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
