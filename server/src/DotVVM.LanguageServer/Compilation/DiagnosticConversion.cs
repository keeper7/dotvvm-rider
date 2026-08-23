using DotVVM.LanguageServer.Analysis;

namespace DotVVM.LanguageServer.Compilation;

/// <summary>
/// One finding of the view compiler, positioned the way the protocol wants it: zero-based, with
/// a real end. Unlike <see cref="ValidationIssue"/> it spans lines - a binding written across
/// several of them is reported as one range, and measured on a real project those occur.
/// </summary>
public record CompilerIssue(
    string Message, DiagnosticLevel Level, int Line, int Character, int EndLine, int EndCharacter);

/// <summary>
/// Turns what the compiler said into positions the protocol understands. Kept apart from the
/// handler because the off-by-one deserves a test of its own: DotVVM counts lines and columns
/// from one, LSP from zero.
/// </summary>
public static class DiagnosticConversion
{
    public static CompilerIssue? ToIssue(CompilerDiagnostic diagnostic)
    {
        // With no position there is nothing to underline, and putting it at the top of the file
        // would point somewhere the reader has no reason to look.
        if (diagnostic.StartLine is not int startLine ||
            diagnostic.StartColumn is not int startColumn)
        {
            return null;
        }

        var endLine = diagnostic.EndLine ?? startLine;
        var endColumn = diagnostic.EndColumn ?? startColumn;

        // An empty range underlines nothing. DotVVM reports one for an unfinished tag, where it
        // means "here", so one character is what shows it.
        if (endLine == startLine && endColumn <= startColumn) endColumn = startColumn + 1;

        return new CompilerIssue(
            Message: diagnostic.Message,
            Level: diagnostic.Severity switch
            {
                "Error" => DiagnosticLevel.Error,
                "Warning" => DiagnosticLevel.Warning,
                _ => DiagnosticLevel.Information,
            },
            Line: Math.Max(0, startLine - 1),
            Character: Math.Max(0, startColumn - 1),
            EndLine: Math.Max(0, endLine - 1),
            EndCharacter: Math.Max(0, endColumn - 1));
    }

    /// <summary>
    /// Everything worth showing. `Hidden` diagnostics are DotVVM's own bookkeeping and a warning
    /// with no message at all turns up next to an unfinished tag - measured on a rewritten
    /// fixture - so neither reaches the editor.
    /// </summary>
    public static IReadOnlyList<CompilerIssue> ToIssues(
        IEnumerable<CompilerDiagnostic> diagnostics)
    {
        var issues = diagnostics
            .Where(d => d.Severity != "Hidden" && !string.IsNullOrWhiteSpace(d.Message))
            .Select(ToIssue)
            .OfType<CompilerIssue>()
            .ToList();

        return issues.Where(issue => !IsSupersededIn(issue, issues)).ToList();
    }

    /// <summary>
    /// Whether a narrower finding says the same thing better. DotVVM reports a wrong identifier
    /// twice: once on the identifier itself - `Could not resolve identifier 'Namxe'`, columns
    /// 31 to 36 - and once across the whole binding, as `Could not initialize binding '…',
    /// requirements … were not met`. The second underlines everything from the opening quote and
    /// adds nothing, so it goes. Both carry the same Priority, so the framework offers no way to
    /// tell them apart; nesting does, and unlike matching on the wording it survives a change of
    /// phrasing between versions.
    /// </summary>
    private static bool IsSupersededIn(CompilerIssue issue, IReadOnlyList<CompilerIssue> all)
    {
        var others = all.Where(other => other != issue && other.Level <= issue.Level).ToList();

        if (others.Any(other => IsInside(other, issue))) return true;

        // The summary also turns up with exactly the same range as the finding it summarises -
        // an unfinished tag produces that pair - and nesting cannot separate those. Its wording
        // is the only thing left to go by, so it is matched, but only ever dropped when
        // something else covers the same place. Alone it is all the user would get.
        return issue.Message.StartsWith(BindingSummary, StringComparison.Ordinal) &&
               others.Any(other => Overlaps(other, issue));
    }

    /// <summary>
    /// How DotVVM opens the summary it emits beside the real cause: "Could not initialize
    /// binding '…', requirements … were not met."
    /// </summary>
    private const string BindingSummary = "Could not initialize binding";

    private static bool Overlaps(CompilerIssue a, CompilerIssue b)
    {
        var aStartsAfterBEnds = a.Line > b.EndLine ||
                                (a.Line == b.EndLine && a.Character > b.EndCharacter);
        var bStartsAfterAEnds = b.Line > a.EndLine ||
                                (b.Line == a.EndLine && b.Character > a.EndCharacter);

        return !aStartsAfterBEnds && !bStartsAfterAEnds;
    }

    /// <summary>Strictly inside: an identical range is a second finding, not a repetition.</summary>
    private static bool IsInside(CompilerIssue inner, CompilerIssue outer)
    {
        var startsAfter = inner.Line > outer.Line ||
                          (inner.Line == outer.Line && inner.Character > outer.Character);
        var endsBefore = inner.EndLine < outer.EndLine ||
                         (inner.EndLine == outer.EndLine && inner.EndCharacter < outer.EndCharacter);
        var startsAtOrAfter = inner.Line > outer.Line ||
                              (inner.Line == outer.Line && inner.Character >= outer.Character);
        var endsAtOrBefore = inner.EndLine < outer.EndLine ||
                             (inner.EndLine == outer.EndLine && inner.EndCharacter <= outer.EndCharacter);

        return (startsAfter && endsAtOrBefore) || (startsAtOrAfter && endsBefore);
    }
}
