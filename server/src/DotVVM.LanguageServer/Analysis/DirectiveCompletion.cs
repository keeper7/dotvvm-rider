using DotVVM.LanguageServer.Model;

namespace DotVVM.LanguageServer.Analysis;

/// <summary>One thing a directive's value may be.</summary>
public record DirectiveSuggestion(string Label, string? Detail = null, string? SortText = null);

/// <summary>
/// Decides what a directive's value may be. Free of protocol types, the same split as
/// <see cref="ControlCompletion"/>: <see cref="DirectiveContextScanner"/> says where the caret
/// is, this says what belongs there.
///
/// Measured on a real project of 244 views, four directives account for 713 of the 715
/// occurrences: @viewModel (244), @import (226), @masterPage (176) and @baseType (67). The
/// rest are served only as far as they cost nothing.
/// </summary>
public static class DirectiveCompletion
{
    public static IReadOnlyList<DirectiveSuggestion> Suggest(
        ControlRegistry registry, DirectiveContext context) => context.Name switch
    {
        "viewModel" => Types(registry.Types.ViewModels, "view model"),
        "baseType" => Types(registry.Controls.Select(c => c.FullTypeName), "control"),
        "import" or "resourceNamespace" => Namespaces(registry.Types.Namespaces),

        // @service and @resourceType name any type of the project, which the registry does not
        // hold - it knows controls and view models, not the rest. Two occurrences in a real
        // project, so this is a decision, not an oversight.
        _ => Array.Empty<DirectiveSuggestion>(),
    };

    private static IReadOnlyList<DirectiveSuggestion> Types(
        IEnumerable<string> names, string detail) =>
        names
            .Select(n => new DirectiveSuggestion(n, detail, SortKey(n)))
            .ToList();

    /// <summary>
    /// Namespaces sorted shortest first: the one a file imports is most often the outermost,
    /// and an alphabetic list would bury it under its own children.
    /// </summary>
    private static IReadOnlyList<DirectiveSuggestion> Namespaces(IEnumerable<string> names) =>
        names
            .Select(n => new DirectiveSuggestion(n, "namespace", SortKey(n)))
            .ToList();

    /// <summary>
    /// Sorts by depth first, then alphabetically. The depth is zero-padded so that "2" does not
    /// fall after "10" the way a plain string comparison would put it.
    /// </summary>
    private static string SortKey(string name) =>
        $"{name.Count(c => c == '.'):D3}{name}";
}
