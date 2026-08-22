namespace DotVVM.LanguageServer.Model;

/// <summary>
/// The project's own types, as far as a directive cares: which view models exist and which
/// namespaces can be imported. Controls live in <see cref="ControlInfo"/>; this is the rest,
/// and only the assembly probe can fill it — neither the built-in defaults nor the serialized
/// config carry it.
/// </summary>
public sealed record ProjectTypes(
    IReadOnlyList<string> ViewModels,
    IReadOnlyList<string> Namespaces)
{
    public static readonly ProjectTypes Empty = new([], []);

    public ProjectTypes MergedWith(ProjectTypes other) =>
        new(ViewModels.Concat(other.ViewModels).Distinct().ToList(),
            Namespaces.Concat(other.Namespaces).Distinct().ToList());
}
