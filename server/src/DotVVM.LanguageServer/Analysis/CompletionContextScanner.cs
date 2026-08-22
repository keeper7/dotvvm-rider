namespace DotVVM.LanguageServer.Analysis;

/// <summary>What the caret is in a position to complete.</summary>
public enum CompletionTarget { None, TagPrefix, TagName, AttributeName }

/// <summary>
/// What the caret is completing, and what is around it. WrittenAttributes holds the attributes
/// already on the tag, without the one being typed.
/// </summary>
public record CompletionContext(
    CompletionTarget Target,
    string? Prefix = null,
    string? TagName = null,
    IReadOnlyList<string>? WrittenAttributes = null)
{
    public static readonly CompletionContext None = new(CompletionTarget.None);
}

/// <summary>
/// Decides what the caret is completing. It walks the text forward as a state machine rather
/// than looking backwards from the caret: a third of the prefixed tags in a real project span
/// several lines, and a tag being typed has no closing '&gt;' yet, so neither can be found by
/// searching the caret's own line.
///
/// Free of both LSP and DotVVM dependencies.
/// </summary>
public static class CompletionContextScanner
{
    public static CompletionContext Detect(string text, int line, int character)
    {
        var offset = OffsetOf(text, line, character);
        var i = 0;

        while (i < offset)
        {
            if (SkipBlock(text, ref i, "<!--", "-->")) continue;
            if (SkipBlock(text, ref i, "<%--", "--%>")) continue;

            if (text[i] != '<') { i++; continue; }

            var end = EndOfTag(text, i);

            if (Matches(text, i, "<!") || Matches(text, i, "<?") || Matches(text, i, "</"))
            {
                i = end < 0 ? text.Length : end;
                continue;
            }

            // A tag that is not closed yet holds the caret: that is the normal state of the one
            // being typed, and its end must not be mistaken for a position before the caret.
            if (end >= 0 && end <= offset) { i = end; continue; }

            return InsideTag(text, i, offset);
        }

        return CompletionContext.None;
    }

    /// <summary>
    /// The LSP position converted to an index. A character index counts within the line, so a
    /// trailing carriage return never gets in the way.
    /// </summary>
    private static int OffsetOf(string text, int line, int character)
    {
        var offset = 0;
        for (var seen = 0; seen < line; seen++)
        {
            var next = text.IndexOf('\n', offset);
            if (next < 0) return text.Length;
            offset = next + 1;
        }
        return Math.Min(offset + character, text.Length);
    }

    private static CompletionContext InsideTag(string text, int start, int offset)
    {
        var nameEnd = start + 1;
        while (nameEnd < text.Length && IsNameChar(text[nameEnd])) nameEnd++;

        var name = text[(start + 1)..nameEnd];
        var colon = name.IndexOf(':');

        if (offset <= nameEnd)
        {
            // still inside the name itself
            return colon >= 0 && offset > start + 1 + colon
                ? new CompletionContext(CompletionTarget.TagName, name[..colon])
                : new CompletionContext(CompletionTarget.TagPrefix);
        }

        var written = new List<string>();
        var i = nameEnd;

        while (i < offset)
        {
            while (i < offset && char.IsWhiteSpace(text[i])) i++;
            if (i >= offset) break;

            if (text[i] is '/' or '>') { i++; continue; }

            var attributeStart = i;
            while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] is not ('=' or '>' or '/')) i++;

            // The caret sits inside this name: it is what is being typed, not what is written
            if (i >= offset) break;

            var attribute = text[attributeStart..i];

            var j = i;
            while (j < text.Length && char.IsWhiteSpace(text[j])) j++;

            if (j < text.Length && text[j] == '=')
            {
                j++;
                while (j < text.Length && char.IsWhiteSpace(text[j])) j++;
                var valueEnd = EndOfValue(text, j);
                if (offset < valueEnd) return CompletionContext.None;
                j = valueEnd;
            }

            i = Math.Max(j, i + 1);
            if (attribute.Length > 0) written.Add(attribute);
        }

        return new CompletionContext(
            CompletionTarget.AttributeName,
            colon >= 0 ? name[..colon] : null,
            colon >= 0 ? name[(colon + 1)..] : name,
            written);
    }

    /// <summary>
    /// The index just past the tag's '&gt;', or -1 when the tag is not closed - which is the
    /// normal state of the tag the user is typing into, and the caller must tell the two apart.
    /// </summary>
    private static int EndOfTag(string text, int start)
    {
        var quote = '\0';
        for (var i = start + 1; i < text.Length; i++)
        {
            var c = text[i];
            if (quote != '\0') { if (c == quote) quote = '\0'; continue; }
            if (c is '"' or '\'') { quote = c; continue; }
            if (c == '>') return i + 1;
        }
        return -1;
    }

    /// <summary>The index just past the attribute value, quoted or bare.</summary>
    private static int EndOfValue(string text, int start)
    {
        if (start >= text.Length) return text.Length;

        if (text[start] is '"' or '\'')
        {
            var end = text.IndexOf(text[start], start + 1);
            return end < 0 ? text.Length : end + 1;
        }

        var i = start;
        while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] is not ('>' or '/')) i++;
        return i;
    }

    private static bool SkipBlock(string text, ref int i, string open, string close)
    {
        if (!Matches(text, i, open)) return false;

        var end = text.IndexOf(close, i, StringComparison.Ordinal);
        i = end < 0 ? text.Length : end + close.Length;
        return true;
    }

    private static bool IsNameChar(char c) =>
        char.IsLetterOrDigit(c) || c is '_' or ':' or '.' or '-';

    private static bool Matches(string text, int at, string what) =>
        at + what.Length <= text.Length &&
        string.CompareOrdinal(text, at, what, 0, what.Length) == 0;
}
