namespace DotVVM.LanguageServer.Analysis;

/// <summary>
/// Where the caret stands in the file header. Name is null unless it is in a directive's value.
/// </summary>
public record DirectiveContext(string? Name = null, string WrittenValue = "")
{
    public static readonly DirectiveContext None = new();
}

/// <summary>
/// Says whether the caret stands in a directive's value, and in which directive's.
///
/// The directive block is the top of the file: blank lines do not end it, anything that is not
/// a directive does. Free of both LSP and DotVVM dependencies, the same split as
/// <see cref="CompletionContextScanner"/> — this one decides *where* the caret is, and
/// <see cref="DirectiveCompletion"/> decides what may be written there.
/// </summary>
public static class DirectiveContextScanner
{
    /// <summary>U+FEFF is not whitespace to .NET, and every real file starts with one.</summary>
    private static readonly char[] Leading = [' ', '\t', '﻿'];

    public static DirectiveContext Detect(string text, int line, int character)
    {
        var lines = text.Split('\n');
        if (line < 0 || line >= lines.Length) return DirectiveContext.None;

        // Everything above the caret has to be a directive or blank, or the header has ended
        for (var i = 0; i < line; i++)
        {
            var above = lines[i].TrimEnd('\r').TrimStart(Leading);
            if (above.Length == 0) continue;
            if (!above.StartsWith('@')) return DirectiveContext.None;
        }

        var current = lines[line].TrimEnd('\r');
        var content = current.TrimStart(Leading);
        var indent = current.Length - content.Length;
        if (!content.StartsWith('@')) return DirectiveContext.None;

        var nameEnd = content.IndexOfAny([' ', '\t']);
        if (nameEnd < 0) return DirectiveContext.None;       // still typing the name itself

        // The caret can be past the end of the line: the editor asks about a column that an
        // edit has already removed
        var offset = Math.Min(character - indent, content.Length);
        if (offset <= nameEnd) return DirectiveContext.None; // the caret is on the name

        var name = content[1..nameEnd];

        // Only what stands before the caret is a prefix; completing over a value replaces
        // the rest of it
        var beforeCaret = content[nameEnd..offset];

        // The part after a comma names the assembly, not the type
        if (beforeCaret.Contains(',')) return DirectiveContext.None;

        // `@service alias = Type` — a type stands only to the right of the '='
        var equals = beforeCaret.LastIndexOf('=');
        if (equals >= 0) beforeCaret = beforeCaret[(equals + 1)..];

        return new DirectiveContext(name, beforeCaret.Trim());
    }
}
