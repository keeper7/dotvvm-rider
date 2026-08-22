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
        var assembly = FindProjectAssembly(projectDir);
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
        var wanted = ReadTargetFramework(targetAssembly);

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

    /// <summary>
    /// Reads the TFM from the runtimeconfig.json next to the assembly, for example "net9.0".
    /// Public for testability.
    /// </summary>
    public static string? ReadTargetFramework(string assemblyPath)
    {
        var config = Path.ChangeExtension(assemblyPath, ".runtimeconfig.json");
        if (!File.Exists(config)) return null;

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(config));
            return doc.RootElement.TryGetProperty("runtimeOptions", out var options) &&
                   options.TryGetProperty("tfm", out var tfm) &&
                   tfm.ValueKind == JsonValueKind.String
                ? tfm.GetString()
                : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return null;
        }
    }

    /// <summary>Finds the newest compiled assembly of the project under bin/.</summary>
    private static string? FindProjectAssembly(string projectDir)
    {
        var root = ProjectRoot.Find(projectDir);
        if (root is null) return null;

        var projectName = Path.GetFileNameWithoutExtension(
            Directory.EnumerateFiles(root, "*.csproj").FirstOrDefault());
        if (projectName is null) return null;

        var bin = Path.Combine(root, "bin");
        if (!Directory.Exists(bin)) return null;

        return Directory
            .EnumerateFiles(bin, projectName + ".dll", SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
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
                ReadProperties(doc.RootElement, "AttachedProperties"));
        }
        catch (JsonException)
        {
            return null;
        }
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
                Properties: ReadProperties(item, "Properties")));
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
