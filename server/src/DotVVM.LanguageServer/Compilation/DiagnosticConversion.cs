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
        IEnumerable<CompilerDiagnostic> diagnostics) =>
        diagnostics
            .Where(d => d.Severity != "Hidden" && !string.IsNullOrWhiteSpace(d.Message))
            .Select(ToIssue)
            .OfType<CompilerIssue>()
            .ToList();
}
