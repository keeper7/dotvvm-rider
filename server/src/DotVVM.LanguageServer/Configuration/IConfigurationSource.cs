using DotVVM.LanguageServer.Model;

namespace DotVVM.LanguageServer.Configuration;

/// <summary>
/// A source of information about controls. The implementations form three tiers of increasing
/// accuracy: built-in values, the serialized config, and the project's assembly.
/// </summary>
public interface IConfigurationSource
{
    /// <summary>Short name shown in the IDE status bar.</summary>
    string Name { get; }

    /// <summary>
    /// Whether the source can see the prefixes registered in the project. Built-in values cannot,
    /// so an unknown prefix must not be reported as an error on their basis.
    /// </summary>
    bool KnowsProjectPrefixes { get; }

    /// <summary>Returns the registry, or null when the source is unavailable.</summary>
    Task<ControlRegistry?> LoadAsync(string projectDir, CancellationToken ct);
}
