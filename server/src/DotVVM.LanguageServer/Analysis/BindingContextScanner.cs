namespace DotVVM.LanguageServer.Analysis;

/// <summary>What the caret is completing inside a binding.</summary>
public enum BindingTarget
{
    None,

    /// <summary>The kind is still being written - `{va` , before the colon.</summary>
    BindingKind,

    /// <summary>
    /// A member: of the data context when Path is empty, of what Path evaluates to otherwise.
    /// </summary>
    Member,
}

/// <summary>
/// Where the caret stands inside a binding expression.
///
/// Path is the member access written to the left of the word being typed - `Customer.Address`
/// in `{value: Customer.Address.Ci|` - and is empty where the word stands on its own, which is
/// where the data context's own members belong.
/// </summary>
public record BindingContext(
    BindingTarget Target,
    string? Kind = null,
    string Path = "",
    string Word = "")
{
    public static readonly BindingContext None = new(BindingTarget.None);
}

/// <summary>
/// Says whether the caret stands inside a binding, which kind is being written and what member
/// access stands to its left. Free of both LSP and DotVVM dependencies, the same split as
/// <see cref="CompletionContextScanner"/>: this decides *where* the caret is, and what may be
/// offered there is decided by <see cref="BindingCompletion"/> for the kinds and by the compiler
/// process for the members, which alone knows the project's types.
///
/// Like the scanner for tags it walks the text forward rather than searching back from the
/// caret: a binding may span several lines, and the one being typed has no closing brace yet -
/// so its end must be told from an end that lies before the caret, not merely looked for.
/// </summary>
public static class BindingContextScanner
{
    /// <summary>
    /// The binding kinds DotVVM knows. controlProperty and controlCommand bind to a markup
    /// control's own properties, so they belong to a .dotcontrol; that is the caller's business,
    /// since a file recognises them by its extension and this class sees only text.
    /// </summary>
    public static readonly string[] Kinds =
    [
        "value", "command", "staticCommand", "resource", "controlProperty", "controlCommand",
    ];

    private static readonly HashSet<string> Known = new(Kinds, StringComparer.Ordinal);

    public static BindingContext Detect(string text, int line, int character)
    {
        var offset = TextPosition.OffsetOf(text, line, character);
        var i = 0;

        // A region that swallows the caret pushes the cursor past it, and the loop ends with
        // nothing found - which is the right answer for a comment or for a script.
        while (i < offset)
        {
            if (Skip(text, ref i, "<!--", "-->") ||
                Skip(text, ref i, "<%--", "--%>") ||
                SkipElement(text, ref i, "script") ||
                SkipElement(text, ref i, "style"))
            {
                continue;
            }

            if (text[i] != '{') { i++; continue; }

            // `{{` counts as one opening only once the caret has passed both braces; with the
            // caret between them the second one is not there yet as far as the author knows.
            var isDouble = i + 1 < text.Length && text[i + 1] == '{' && offset > i + 1;
            var contentStart = i + (isDouble ? 2 : 1);

            var end = FindEnd(text, contentStart, isDouble);
            if (end >= 0 && end <= offset) { i = end; continue; }   // closed before the caret

            var inside = Describe(text[contentStart..offset]);
            if (inside.Target != BindingTarget.None) return inside;

            i++;
        }

        return BindingContext.None;
    }

    /// <summary>
    /// Reads what stands between the opening brace and the caret. Anything that is neither a
    /// kind being typed nor a known kind followed by a colon is some other pair of braces -
    /// text, a style block, a script - and holds nothing to complete.
    /// </summary>
    private static BindingContext Describe(string content)
    {
        // The kind's colon is always the first one: whatever follows it is an expression, and
        // an expression's own colons - a conditional, a named argument - come later.
        var colon = content.IndexOf(':');

        if (colon < 0)
        {
            var word = content.TrimStart();
            return IsIdentifier(word)
                ? new BindingContext(BindingTarget.BindingKind, Word: word)
                : BindingContext.None;
        }

        var kind = content[..colon].Trim();
        if (!Known.Contains(kind)) return BindingContext.None;

        var (readable, path, member) = Split(content[(colon + 1)..]);
        return readable
            ? new BindingContext(BindingTarget.Member, kind, path, member)
            : BindingContext.None;
    }

    /// <summary>
    /// Splits the expression into the member access to the left of the caret and the word being
    /// typed. Reports the expression as unreadable when the caret follows a dot whose left-hand
    /// side cannot be read - a string literal, an unbalanced bracket - because offering the data
    /// context's own members there would answer a question nobody asked.
    /// </summary>
    private static (bool Readable, string Path, string Word) Split(string expression)
    {
        var wordStart = expression.Length;
        while (wordStart > 0 && IsWordChar(expression[wordStart - 1])) wordStart--;
        var word = expression[wordStart..];

        var dot = wordStart - 1;
        if (dot < 0 || expression[dot] != '.') return (true, "", word);

        var e = dot;
        while (true)
        {
            while (e > 0 && char.IsWhiteSpace(expression[e - 1])) e--;

            // A call or an index closes the segment: `Items[0].` and `Name.ToUpper().` are both
            // paths, and both continue with an identifier in front of the bracket.
            while (e > 0 && expression[e - 1] is ')' or ']')
            {
                var opener = MatchingOpener(expression, e - 1);
                if (opener < 0) return (false, "", word);
                e = opener;
            }

            var start = e;
            while (start > 0 && IsWordChar(expression[start - 1])) start--;
            if (start == e) return (false, "", word);
            e = start;

            while (e > 0 && char.IsWhiteSpace(expression[e - 1])) e--;
            if (e > 0 && expression[e - 1] == '.') { e--; continue; }

            return (true, expression[e..dot].Trim(), word);
        }
    }

    /// <summary>
    /// The index of the bracket that opens the one at closeIndex. String literals are not
    /// tracked: read backwards there is no telling an opening quote from a closing one, and a
    /// bracket inside a literal only ever leads to a path that resolves to nothing.
    /// </summary>
    private static int MatchingOpener(string text, int closeIndex)
    {
        var close = text[closeIndex];
        var open = close == ')' ? '(' : '[';
        var depth = 0;

        for (var k = closeIndex; k >= 0; k--)
        {
            if (text[k] == close) depth++;
            else if (text[k] == open && --depth == 0) return k;
        }
        return -1;
    }

    /// <summary>
    /// The index past the end of the binding, or -1 while it is unterminated - which is the
    /// ordinary state of the one being written. Braces nest and a brace inside a string literal
    /// is not one, the same rules the plugin's own scanner follows.
    /// </summary>
    private static int FindEnd(string text, int contentStart, bool isDouble)
    {
        var depth = 1;
        var quote = '\0';

        for (var i = contentStart; i < text.Length; i++)
        {
            var c = text[i];

            if (quote != '\0')
            {
                if (c == '\\') i++;
                else if (c == quote) quote = '\0';
                continue;
            }

            switch (c)
            {
                case '"' or '\'': quote = c; break;
                case '{': depth++; break;
                case '}' when --depth == 0:
                    return isDouble
                        ? (i + 1 < text.Length && text[i + 1] == '}' ? i + 2 : -1)
                        : i + 1;
            }
        }
        return -1;
    }

    private static bool Skip(string text, ref int i, string open, string close)
    {
        if (!Matches(text, i, open)) return false;

        var end = text.IndexOf(close, i, StringComparison.Ordinal);
        i = end < 0 ? text.Length : end + close.Length;
        return true;
    }

    /// <summary>
    /// Skips a script or a style element whole. Braces are the ordinary punctuation of both
    /// languages, and `{ color: red }` is not a binding however much the shape resembles one.
    /// </summary>
    private static bool SkipElement(string text, ref int i, string name)
    {
        if (!Matches(text, i, "<" + name)) return false;

        var after = i + name.Length + 1;
        if (after < text.Length && !char.IsWhiteSpace(text[after]) && text[after] is not ('>' or '/'))
        {
            return false;
        }

        var end = text.IndexOf("</" + name, i, StringComparison.OrdinalIgnoreCase);
        i = end < 0 ? text.Length : end + name.Length + 2;
        return true;
    }

    private static bool IsIdentifier(string word) =>
        word.Length == 0 || (!char.IsDigit(word[0]) && word.All(IsWordChar));

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    private static bool Matches(string text, int at, string what) =>
        at + what.Length <= text.Length &&
        string.Compare(text, at, what, 0, what.Length, StringComparison.OrdinalIgnoreCase) == 0;
}
