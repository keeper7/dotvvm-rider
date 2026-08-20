using System.Diagnostics;
using System.Text.Json;
using DotVVM.LanguageServer.Model;

namespace DotVVM.LanguageServer.Configuration;

/// <summary>
/// Stupeň 3: spustí probe proces, který načte sestavenou assembly projektu
/// a vrátí skutečnou konfiguraci. Dostupné po sestavení projektu.
/// </summary>
public sealed class AssemblyProbeSource : IConfigurationSource
{
    /// <summary>Pevně zadaná cesta k probe; null znamená výběr podle TFM cílové assembly.</summary>
    private readonly string? _fixedProbePath;
    private readonly TimeSpan _timeout;

    /// <summary>Varianty probe od nejstarší; novější runtime načte i starší assembly.</summary>
    private static readonly string[] ProbeFrameworks = { "net8.0", "net9.0" };

    public AssemblyProbeSource(string? probePath = null, TimeSpan? timeout = null)
    {
        _fixedProbePath = probePath;
        _timeout = timeout ?? TimeSpan.FromSeconds(30);
    }

    public string Name => "plná";

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
    /// Vybere variantu probe podle TFM cílové assembly. Probe musí běžet na runtime
    /// alespoň tak novém, jako je cílový projekt — net8.0 host neumí načíst assembly
    /// cílenou na net9.0. Když TFM nejde přečíst, vezme nejnovější dostupnou variantu.
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
                // První varianta, která není starší než cílový projekt
                var match = candidates.FirstOrDefault(
                    c => Array.IndexOf(ProbeFrameworks, c.Tfm) >= index);
                if (match.Path is not null) return match.Path;
            }
        }

        return candidates[^1].Path;
    }

    /// <summary>
    /// Přečte TFM z runtimeconfig.json vedle assembly, například "net9.0".
    /// Veřejné kvůli testovatelnosti.
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

    /// <summary>Najde nejnovější sestavenou assembly projektu v bin/.</summary>
    private static string? FindProjectAssembly(string projectDir)
    {
        var root = FindProjectRoot(projectDir);
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

    private static string? FindProjectRoot(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir is not null)
        {
            if (dir.EnumerateFiles("*.csproj").Any()) return dir.FullName;
            dir = dir.Parent;
        }
        return null;
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
                await Console.Error.WriteLineAsync($"[dotvvm-ls] probe skončil s chybou: {stderr}");
                return null;
            }
            return stdout;
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* už skončil */ }
            return null;
        }
    }

    /// <summary>Veřejné kvůli testovatelnosti bez spouštění procesu.</summary>
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

            return new ControlRegistry(registrations, Array.Empty<ControlInfo>());
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Str(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
