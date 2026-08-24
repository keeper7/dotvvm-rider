namespace DotVVM.LanguageServer.Analysis;

/// <summary>
/// The protocol's line-and-character position turned into an index into the text. A character
/// index counts within its line, so a trailing carriage return never gets in the way.
/// </summary>
public static class TextPosition
{
    public static int OffsetOf(string text, int line, int character)
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
}
