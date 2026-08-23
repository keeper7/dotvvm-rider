namespace DotVVM.LanguageServer.Analysis;

/// <summary>One directive in the file header, with LSP-style zero-based coordinates.</summary>
public record DirectiveOccurrence(
    string Name, string Value, int Line, int Character, int Length);

/// <summary>
/// Finds the directives at the top of a file. The counterpart of the plugin's DirectiveScanner,
/// with one difference: an unknown name does **not** end the block. The validator has to see
/// both the typo and everything that follows it, whereas the plugin only needs to know where a
/// directive is.
///
/// Free of both LSP and DotVVM dependencies.
/// </summary>
public static class DirectiveScanner
{
    /// <summary>
    /// The directives DotVVM defines, from ParserConstants in DotVVM.Framework. The parser
    /// accepts any name at all - @totalNonsense yields a well-formed node with no error, and
    /// only compiling the view rejects it - so this list is where a typo is caught.
    /// </summary>
    public static readonly IReadOnlyList<string> KnownNames =
    [
        "viewModel", "masterPage", "baseType", "resourceType", "resourceNamespace",
        "import", "wrapperTag", "noWrapperTag", "service", "js", "property"
    ];

    /// <summary>U+FEFF is not whitespace to .NET, and every real file starts with one.</summary>
    private static readonly char[] Leading = [' ', '\t', '﻿'];

    public static IReadOnlyList<DirectiveOccurrence> Scan(string text)
    {
        var result = new List<DirectiveOccurrence>();
        var lines = text.Split('\n');

        for (var line = 0; line < lines.Length; line++)
        {
            var raw = lines[line].TrimEnd('\r');
            var content = raw.TrimStart(Leading);
            var indent = raw.Length - content.Length;

            if (content.Length == 0) continue;               // blank lines do not end the block
            if (!content.StartsWith('@')) break;             // the document body has started

            var nameEnd = content.IndexOfAny([' ', '\t']);
            if (nameEnd < 0) nameEnd = content.Length;

            result.Add(new DirectiveOccurrence(
                Name: content[1..nameEnd],
                Value: content[nameEnd..].Trim(),
                Line: line,
                Character: indent,
                Length: nameEnd));
        }

        return result;
    }
}
