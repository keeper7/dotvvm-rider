using DotVVM.LanguageServer.Analysis;
using DotVVM.LanguageServer.Compilation;
using Xunit;

namespace DotVVM.LanguageServer.Tests;

public class DiagnosticConversionTests
{
    private static CompilerDiagnostic At(
        int line, int column, int? endLine = null, int? endColumn = null,
        string severity = "Error", string message = "something") =>
        new(severity, message, line, column, endLine, endColumn);

    /// <summary>DotVVM counts lines and columns from one, the protocol from zero.</summary>
    [Fact]
    public void ShiftsPositionsToZeroBased()
    {
        var issue = DiagnosticConversion.ToIssue(At(8, 38, 8, 48));

        Assert.Equal(7, issue!.Line);
        Assert.Equal(37, issue.Character);
        Assert.Equal(7, issue.EndLine);
        Assert.Equal(47, issue.EndCharacter);
    }

    /// <summary>
    /// A binding written across several lines is reported as one range, and a real project
    /// contains them.
    /// </summary>
    [Fact]
    public void KeepsARangeThatSpansLines()
    {
        var issue = DiagnosticConversion.ToIssue(At(19, 39, 25, 66));

        Assert.Equal(18, issue!.Line);
        Assert.Equal(24, issue.EndLine);
    }

    /// <summary>
    /// DotVVM reports an empty range for an unfinished tag, meaning "here". An empty range
    /// underlines nothing at all, so one character is the least that shows.
    /// </summary>
    [Fact]
    public void WidensAnEmptyRangeToOneCharacter()
    {
        var issue = DiagnosticConversion.ToIssue(At(8, 95, 8, 95));

        Assert.Equal(94, issue!.Character);
        Assert.Equal(95, issue.EndCharacter);
    }

    [Fact]
    public void UsesTheStartWhenThereIsNoEnd()
    {
        var issue = DiagnosticConversion.ToIssue(At(3, 5));

        Assert.Equal(2, issue!.Line);
        Assert.Equal(2, issue.EndLine);
    }

    /// <summary>
    /// Without a position there is nothing to underline, and the top of the file would point
    /// somewhere the reader has no reason to look.
    /// </summary>
    [Fact]
    public void DropsADiagnosticWithNoPosition()
    {
        Assert.Null(DiagnosticConversion.ToIssue(
            new CompilerDiagnostic("Error", "somewhere", null, null, null, null)));
    }

    [Fact]
    public void MapsTheSeverities()
    {
        Assert.Equal(DiagnosticLevel.Error,
                     DiagnosticConversion.ToIssue(At(1, 1, severity: "Error"))!.Level);
        Assert.Equal(DiagnosticLevel.Warning,
                     DiagnosticConversion.ToIssue(At(1, 1, severity: "Warning"))!.Level);
        Assert.Equal(DiagnosticLevel.Information,
                     DiagnosticConversion.ToIssue(At(1, 1, severity: "Info"))!.Level);
    }

    /// <summary>
    /// Hidden diagnostics are DotVVM's own bookkeeping, and a warning with no message turns up
    /// next to an unfinished tag - measured on a rewritten fixture. Neither belongs in an editor.
    /// </summary>
    [Fact]
    public void LeavesOutWhatTheEditorCannotUse()
    {
        var issues = DiagnosticConversion.ToIssues(new[]
        {
            At(1, 1, severity: "Hidden"),
            At(2, 1, message: "   "),
            At(3, 1, message: "a real one"),
        });

        Assert.Equal("a real one", Assert.Single(issues).Message);
    }
}
