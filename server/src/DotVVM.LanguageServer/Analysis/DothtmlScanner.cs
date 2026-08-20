namespace DotVVM.LanguageServer.Analysis;

/// <summary>Výskyt tagu s prefixem, se souřadnicemi v konvenci LSP (od nuly).</summary>
public record TagOccurrence(string Prefix, string TagName, int Line, int Character, int Length);

/// <summary>
/// Najde v textu tagy s prefixem (například &lt;dot:Button&gt;). Nejde o plnohodnotný
/// HTML parser — hledá jen to, co je potřeba pro validaci a completion.
/// Bez závislosti na LSP i na DotVVM.
/// </summary>
public static class DothtmlScanner
{
    public static IReadOnlyList<TagOccurrence> ScanTags(string text)
    {
        var result = new List<TagOccurrence>();
        var line = 0;
        var lineStart = 0;
        var i = 0;

        while (i < text.Length)
        {
            var c = text[i];

            if (c == '\n')
            {
                line++;
                i++;
                lineStart = i;
                continue;
            }

            if (c != '<') { i++; continue; }

            // Komentáře, DOCTYPE a zpracovací instrukce přeskoč celé
            if (Matches(text, i, "<!--"))
            {
                var end = text.IndexOf("-->", i, StringComparison.Ordinal);
                var stop = end < 0 ? text.Length : end + 3;
                (line, lineStart) = CountLines(text, i, stop, line, lineStart);
                i = stop;
                continue;
            }

            if (Matches(text, i, "<!") || Matches(text, i, "<?"))
            {
                var end = text.IndexOf('>', i);
                var stop = end < 0 ? text.Length : end + 1;
                (line, lineStart) = CountLines(text, i, stop, line, lineStart);
                i = stop;
                continue;
            }

            var nameStart = i + 1;
            if (nameStart < text.Length && text[nameStart] == '/')
            {
                i++;                                  // uzavírací tag ignorujeme
                continue;
            }

            var j = nameStart;
            while (j < text.Length && (char.IsLetterOrDigit(text[j]) || text[j] == '_')) j++;

            if (j >= text.Length || text[j] != ':')
            {
                i = nameStart;                        // tag bez prefixu
                continue;
            }

            var prefix = text[nameStart..j];
            var tagStart = j + 1;
            var k = tagStart;
            while (k < text.Length && (char.IsLetterOrDigit(text[k]) || text[k] == '_')) k++;

            if (k == tagStart) { i = nameStart; continue; }

            result.Add(new TagOccurrence(
                Prefix: prefix,
                TagName: text[tagStart..k],
                Line: line,
                Character: nameStart - lineStart,
                Length: k - nameStart));

            i = k;
        }

        return result;
    }

    private static bool Matches(string text, int at, string what) =>
        at + what.Length <= text.Length &&
        string.CompareOrdinal(text, at, what, 0, what.Length) == 0;

    private static (int Line, int LineStart) CountLines(
        string text, int from, int to, int line, int lineStart)
    {
        for (var i = from; i < to && i < text.Length; i++)
        {
            if (text[i] != '\n') continue;
            line++;
            lineStart = i + 1;
        }
        return (line, lineStart);
    }
}
