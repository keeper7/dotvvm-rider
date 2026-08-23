using DotVVM.LanguageServer.Model;

namespace DotVVM.LanguageServer.Analysis;

/// <summary>
/// Reports what DotVVM will refuse in a file's header, and a few things it passes over in
/// silence although they have no effect.
///
/// The messages follow the framework's own, measured by running its IControlTreeResolver over
/// broken headers. Free of both LSP and DotVVM dependencies, the same split as TagValidator.
/// </summary>
public static class DirectiveValidator
{
    /// <summary>
    /// The directives DotVVM keeps one of. Measured from MarkupPageMetadata: Imports,
    /// InjectedServices and Properties are lists, everything else is a single value, and the
    /// resolver answers a second one with "Directive 'x' cannot be present multiple times."
    /// </summary>
    private static readonly HashSet<string> SingleValued = new(StringComparer.Ordinal)
    {
        "viewModel", "masterPage", "baseType", "js",
        "wrapperTag", "noWrapperTag", "resourceType", "resourceNamespace"
    };

    /// <summary>The one directive that is a flag and carries nothing.</summary>
    private static readonly HashSet<string> TakesNoValue = new(StringComparer.Ordinal)
    {
        "noWrapperTag"
    };

    public static IReadOnlyList<ValidationIssue> Validate(
        string text, string fileName, ControlRegistry registry)
    {
        var issues = new List<ValidationIssue>();
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var directive in DirectiveScanner.Scan(text))
        {
            if (!DirectiveScanner.KnownNames.Contains(directive.Name, StringComparer.Ordinal))
            {
                issues.Add(Issue(directive, DiagnosticLevel.Error,
                    $"Unknown directive '@{directive.Name}'. DotVVM ignores it silently, so it " +
                    "will simply have no effect."));
                continue;
            }

            seen.TryGetValue(directive.Name, out var count);
            seen[directive.Name] = count + 1;

            if (count > 0 && SingleValued.Contains(directive.Name))
            {
                issues.Add(Issue(directive, DiagnosticLevel.Error,
                    $"Directive '{directive.Name}' cannot be present multiple times."));
                continue;
            }

            if (TakesNoValue.Contains(directive.Name))
            {
                if (directive.Value.Length > 0)
                    issues.Add(Issue(directive, DiagnosticLevel.Warning,
                        $"'@{directive.Name}' takes no value; '{directive.Value}' is ignored."));
                continue;
            }

            if (directive.Value.Length == 0)
            {
                issues.Add(Issue(directive, DiagnosticLevel.Error,
                    $"'@{directive.Name}' is missing its value."));
                continue;
            }

            // DotVVM: "Assignment operation expected - the correct form is
            // `@service myStringService = ISomeService<string>`"
            if (directive.Name == "service" && !directive.Value.Contains('='))
                issues.Add(Issue(directive, DiagnosticLevel.Error,
                    "Assignment expected: the form is '@service alias = SomeService'."));
        }

        return issues;
    }

    private static ValidationIssue Issue(
        DirectiveOccurrence directive, DiagnosticLevel level, string message) =>
        new(message, level, directive.Line, directive.Character, directive.Length);
}
