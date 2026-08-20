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
    private readonly string _probePath;
    private readonly TimeSpan _timeout;

    public AssemblyProbeSource(string? probePath = null, TimeSpan? timeout = null)
    {
        _probePath = probePath ?? DefaultProbePath();
        _timeout = timeout ?? TimeSpan.FromSeconds(30);
    }

    public string Name => "plná";

    private static string DefaultProbePath() =>
        Path.Combine(AppContext.BaseDirectory, "probe", "DotVVM.LanguageServer.Probe.dll");

    public async Task<ControlRegistry?> LoadAsync(string projectDir, CancellationToken ct)
    {
        if (!File.Exists(_probePath)) return null;

        var assembly = FindProjectAssembly(projectDir);
        if (assembly is null) return null;

        var output = await RunProbeAsync(assembly, projectDir, ct);
        return output is null ? null : ParseProbeOutput(output);
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

    private async Task<string?> RunProbeAsync(string assembly, string projectDir, CancellationToken ct)
    {
        var info = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        info.ArgumentList.Add(_probePath);
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
