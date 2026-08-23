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

    /// <summary>
    /// The directives that shape a markup control. In a view they are quietly ignored - the
    /// resolver reports nothing at all - so they are worth a warning: nothing tells the user
    /// otherwise. @baseType is the exception and is handled as an error, because the framework
    /// does complain there.
    /// </summary>
    private static readonly HashSet<string> MarkupControlOnly = new(StringComparer.Ordinal)
    {
        "wrapperTag", "noWrapperTag", "property"
    };

    /// <summary>A view and a master page have one; a markup control does not.</summary>
    private const string MasterPageOnly = "masterPage";

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

            var isControl = fileName.EndsWith(".dotcontrol", StringComparison.OrdinalIgnoreCase);

            if (MarkupControlOnly.Contains(directive.Name) && !isControl)
            {
                issues.Add(Issue(directive, DiagnosticLevel.Warning,
                    $"'@{directive.Name}' shapes a markup control and has no effect here."));
                continue;
            }

            // DotVVM: "Markup controls must derive from DotvvmMarkupControl class!"
            if (directive.Name == "baseType" && !isControl)
            {
                issues.Add(Issue(directive, DiagnosticLevel.Error,
                    "'@baseType' belongs to a markup control; a view derives from DotvvmView."));
                continue;
            }

            if (directive.Name == MasterPageOnly && isControl)
            {
                issues.Add(Issue(directive, DiagnosticLevel.Warning,
                    "'@masterPage' has no effect in a markup control."));
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

        // DotVVM: "The @viewModel directive is missing in the page 'Test.dothtml'!" - it holds
        // for a markup control too. An empty file is spared: the user has only just made it.
        if (!seen.ContainsKey("viewModel") && text.Trim().Length > 0)
            issues.Add(new ValidationIssue(
                $"The @viewModel directive is missing in '{Path.GetFileName(fileName)}'.",
                DiagnosticLevel.Error, 0, 0, 0));

        return issues;
    }

    private static ValidationIssue Issue(
        DirectiveOccurrence directive, DiagnosticLevel level, string message) =>
        new(message, level, directive.Line, directive.Character, directive.Length);
}
