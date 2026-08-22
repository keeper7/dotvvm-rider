using DotVVM.LanguageServer.Model;

namespace DotVVM.LanguageServer.Configuration;

/// <summary>The loaded configuration together with the name of the source used.</summary>
public record ConfigurationResult(
    ControlRegistry Registry, string SourceName, bool KnowsProjectPrefixes);

/// <summary>
/// Composes the sources from the least to the most accurate. A higher tier does not replace a
/// lower one but adds to it, so the standard controls stay known even when the higher tier covers
/// only part of the project.
/// </summary>
public sealed class ProjectConfigurationProvider
{
    private readonly IReadOnlyList<IConfigurationSource> _sources;

    /// <param name="sources">Sources ordered from the lowest tier to the highest.</param>
    public ProjectConfigurationProvider(IEnumerable<IConfigurationSource> sources)
    {
        _sources = sources.ToList();
    }

    /// <summary>Default composition: built-in values, serialized config, project assembly.</summary>
    public static ProjectConfigurationProvider CreateDefault() =>
        new(new IConfigurationSource[]
        {
            new BuiltInDefaults(),
            new SerializedConfigSource(),
            new AssemblyProbeSource()
        });

    public async Task<ConfigurationResult> GetAsync(string projectDir, CancellationToken ct)
    {
        var registry = ControlRegistry.Empty;
        var sourceName = "none";
        var knowsProjectPrefixes = false;

        foreach (var source in _sources)
        {
            ControlRegistry? loaded;
            try
            {
                loaded = await source.LoadAsync(projectDir, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One source failing must not bring down the whole provider
                await Console.Error.WriteLineAsync(
                    $"[dotvvm-ls] source '{source.Name}' failed: {ex.Message}");
                continue;
            }

            if (loaded is null) continue;

            registry = registry.MergedWith(loaded);
            sourceName = source.Name;
            knowsProjectPrefixes |= source.KnowsProjectPrefixes;
        }

        // Only now are all the sources merged, so a markup control can be matched against a type
        // that a different tier contributed.
        var root = ProjectRoot.Find(projectDir);
        if (root is not null)
        {
            registry = MarkupControlResolver.Resolve(registry, root, MarkupControlResolver.ReadFile);
        }

        return new ConfigurationResult(registry, sourceName, knowsProjectPrefixes);
    }
}
