using System.Text.Json;

namespace DotVVM.LanguageServer.Configuration;

/// <summary>
/// Finding the project's compiled assembly. Both the probe and the view compiler need it, and
/// both need to know which framework it targets.
/// </summary>
public static class ProjectAssembly
{
    /// <summary>The newest compiled assembly of the project under bin/.</summary>
    public static string? Find(string projectDir)
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

    /// <summary>
    /// Reads the framework from the runtimeconfig.json next to the assembly, for example "net9.0".
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
}
