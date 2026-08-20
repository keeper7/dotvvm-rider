using DotVVM.LanguageServer.Model;

namespace DotVVM.LanguageServer.Configuration;

/// <summary>
/// Zdroj informací o kontrolkách. Implementace tvoří tři stupně rostoucí přesnosti:
/// vestavěné hodnoty, serialized config, assembly projektu.
/// </summary>
public interface IConfigurationSource
{
    /// <summary>Krátký název zobrazovaný ve status baru IDE.</summary>
    string Name { get; }

    /// <summary>
    /// Zda zdroj vidí prefixy registrované v projektu. Vestavěné hodnoty je nevidí,
    /// takže na jejich základě nelze hlásit neznámý prefix jako chybu.
    /// </summary>
    bool KnowsProjectPrefixes { get; }

    /// <summary>Vrátí registry, nebo null, není-li zdroj k dispozici.</summary>
    Task<ControlRegistry?> LoadAsync(string projectDir, CancellationToken ct);
}
