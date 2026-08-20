using DotVVM.LanguageServer.Model;

namespace DotVVM.LanguageServer.Configuration;

/// <summary>Výsledek načtení konfigurace včetně názvu použitého stupně.</summary>
public record ConfigurationResult(ControlRegistry Registry, string SourceName);

/// <summary>
/// Skládá zdroje od nejméně po nejvíce přesný. Vyšší stupeň nenahrazuje nižší,
/// ale doplňuje ho — díky tomu zůstanou známé i standardní kontrolky v případě,
/// že vyšší stupeň zná jen část projektu.
/// </summary>
public sealed class ProjectConfigurationProvider
{
    private readonly IReadOnlyList<IConfigurationSource> _sources;

    /// <param name="sources">Zdroje seřazené od nejnižšího stupně k nejvyššímu.</param>
    public ProjectConfigurationProvider(IEnumerable<IConfigurationSource> sources)
    {
        _sources = sources.ToList();
    }

    /// <summary>Výchozí složení: vestavěné hodnoty → serialized config → assembly projektu.</summary>
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
        var sourceName = "žádná";

        foreach (var source in _sources)
        {
            ControlRegistry? loaded;
            try
            {
                loaded = await source.LoadAsync(projectDir, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Selhání jednoho zdroje nesmí shodit celý provider
                await Console.Error.WriteLineAsync(
                    $"[dotvvm-ls] zdroj '{source.Name}' selhal: {ex.Message}");
                continue;
            }

            if (loaded is null) continue;

            registry = registry.MergedWith(loaded);
            sourceName = source.Name;
        }

        return new ConfigurationResult(registry, sourceName);
    }
}
