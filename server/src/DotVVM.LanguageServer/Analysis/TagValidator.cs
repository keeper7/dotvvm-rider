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
    /// <param name="knowsProjectPrefixes">
    /// Zda registr pochází ze zdroje, který zná prefixy registrované v projektu.
    /// Vestavěné výchozí hodnoty je znát nemohou, takže na jejich základě nelze
    /// cizí prefix prohlásit za chybu — uživatel by dostal chybu za správný kód.
    /// </param>
    public static IReadOnlyList<ValidationIssue> Validate(
        string text, ControlRegistry registry, bool knowsProjectPrefixes)
    {
        // Prázdný registr znamená, že o projektu nic nevíme. Hlásit v takové situaci
        // chyby by znamenalo zaplavit uživatele falešnými poplachy.
        if (registry.AllPrefixes.Count == 0) return Array.Empty<ValidationIssue>();

        var issues = new List<ValidationIssue>();

        foreach (var tag in DothtmlScanner.ScanTags(text))
        {
            if (!registry.IsKnownPrefix(tag.Prefix))
            {
                // Bez znalosti projektových prefixů mlčíme; proč, vysvětluje status bar.
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
