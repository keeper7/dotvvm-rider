using DotVVM.LanguageServer.Model;

namespace DotVVM.LanguageServer.Analysis;

public enum DiagnosticLevel { Error, Warning, Information }

public record ValidationIssue(
    string Message, DiagnosticLevel Level, int Line, int Character, int Length);

/// <summary>
/// Checks prefixed tags against the control registry.
/// Free of LSP dependencies; the handler converts to protocol types.
/// </summary>
public static class TagValidator
{
    /// <param name="knowsProjectPrefixes">
    /// Whether the registry came from a source that knows the prefixes registered in the
    /// project. Built-in defaults cannot know them, so a foreign prefix must not be declared an
    /// error on their basis: the user would get an error for correct code.
    /// </param>
    public static IReadOnlyList<ValidationIssue> Validate(
        string text, ControlRegistry registry, bool knowsProjectPrefixes)
    {
        // An empty registry means we know nothing about the project. Reporting errors in that
        // state would flood the user with false alarms.
        if (registry.AllPrefixes.Count == 0) return Array.Empty<ValidationIssue>();

        var issues = new List<ValidationIssue>();

        foreach (var tag in DothtmlScanner.ScanTags(text))
        {
            if (!registry.IsKnownPrefix(tag.Prefix))
            {
                // Without knowledge of the project's prefixes we stay silent; the status bar explains why.
                if (!knowsProjectPrefixes) continue;

                issues.Add(new ValidationIssue(
                    $"Unknown control prefix '{tag.Prefix}'. Register it in DotvvmStartup.",
                    DiagnosticLevel.Error, tag.Line, tag.Character, tag.Length));
                continue;
            }

            if (!registry.IsKnownTag(tag.Prefix, tag.TagName))
            {
                issues.Add(new ValidationIssue(
                    $"Control '{tag.Prefix}:{tag.TagName}' was not found.",
                    DiagnosticLevel.Error, tag.Line, tag.Character, tag.Length));
            }
        }

        return issues;
    }
}
