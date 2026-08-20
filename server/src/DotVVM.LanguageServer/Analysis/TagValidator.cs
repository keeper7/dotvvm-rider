using DotVVM.LanguageServer.Model;

namespace DotVVM.LanguageServer.Analysis;

public enum DiagnosticLevel { Error, Warning, Information }

public record ValidationIssue(
    string Message, DiagnosticLevel Level, int Line, int Character, int Length);

/// <summary>
/// Kontroluje tagy s prefixem proti registru kontrolek.
/// Bez závislosti na LSP — překlad na protokolové typy dělá handler.
/// </summary>
public static class TagValidator
{
    public static IReadOnlyList<ValidationIssue> Validate(string text, ControlRegistry registry)
    {
        // Prázdný registr znamená, že o projektu nic nevíme. Hlásit v takové situaci
        // chyby by znamenalo zaplavit uživatele falešnými poplachy.
        if (registry.AllPrefixes.Count == 0) return Array.Empty<ValidationIssue>();

        var issues = new List<ValidationIssue>();

        foreach (var tag in DothtmlScanner.ScanTags(text))
        {
            if (!registry.IsKnownPrefix(tag.Prefix))
            {
                issues.Add(new ValidationIssue(
                    $"Neznámý prefix kontrolky '{tag.Prefix}'. Zaregistruj ho v DotvvmStartup.",
                    DiagnosticLevel.Error, tag.Line, tag.Character, tag.Length));
                continue;
            }

            if (!registry.IsKnownTag(tag.Prefix, tag.TagName))
            {
                issues.Add(new ValidationIssue(
                    $"Kontrolka '{tag.Prefix}:{tag.TagName}' nebyla nalezena.",
                    DiagnosticLevel.Error, tag.Line, tag.Character, tag.Length));
            }
        }

        return issues;
    }
}
