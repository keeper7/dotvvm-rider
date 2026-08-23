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

/// <summary>
/// DotVVM reports a wrong identifier twice: on the identifier, and again across the whole
/// binding as "requirements … were not met". The second underlines from the opening quote and
/// says nothing more, which is what the user saw.
/// </summary>
public class NestedDiagnosticTests
{
    private static CompilerDiagnostic At(int startColumn, int endColumn, string message) =>
        new("Error", message, 8, startColumn, 8, endColumn);

    [Fact]
    public void KeepsOnlyTheNarrowerOfTwoFindingsInTheSamePlace()
    {
        var issues = DiagnosticConversion.ToIssues(new[]
        {
            At(31, 36, "Could not resolve identifier 'Namxe'."),
            At(23, 37, "Could not initialize binding '{value: Namxe}', requirements … were not met."),
        });

        var kept = Assert.Single(issues);
        Assert.StartsWith("Could not resolve identifier", kept.Message);
        Assert.Equal(30, kept.Character);
        Assert.Equal(35, kept.EndCharacter);
    }

    [Fact]
    public void KeepsBothWhenNeitherContainsTheOther()
    {
        var issues = DiagnosticConversion.ToIssues(new[]
        {
            At(10, 20, "first"),
            At(30, 40, "second"),
        });

        Assert.Equal(2, issues.Count);
    }

    /// <summary>An identical range is a second finding about one place, not a repetition of it.</summary>
    [Fact]
    public void KeepsBothWhenTheRangesAreEqual()
    {
        var issues = DiagnosticConversion.ToIssues(new[]
        {
            At(10, 20, "first"),
            At(10, 20, "second"),
        });

        Assert.Equal(2, issues.Count);
    }

    /// <summary>
    /// A range sharing one edge still counts as inside - the binding's own start is where the
    /// summary begins, and the identifier can sit right at it.
    /// </summary>
    [Fact]
    public void CountsARangeSharingOneEdgeAsInside()
    {
        var issues = DiagnosticConversion.ToIssues(new[]
        {
            At(23, 30, "narrow, starts together"),
            At(23, 37, "wide"),
        });

        Assert.Equal("narrow, starts together", Assert.Single(issues).Message);
    }

    /// <summary>A warning must not silence an error that happens to sit inside it.</summary>
    [Fact]
    public void NeverLetsAWarningSilenceAnErrorAroundIt()
    {
        var issues = DiagnosticConversion.ToIssues(new[]
        {
            new CompilerDiagnostic("Warning", "narrow warning", 8, 31, 8, 36),
            new CompilerDiagnostic("Error", "wide error", 8, 23, 8, 37),
        });

        Assert.Equal(2, issues.Count);
    }
}

/// <summary>
/// The summary DotVVM emits beside a real cause sometimes carries exactly the same range, which
/// nesting cannot tell apart - an unfinished tag produces such a pair.
/// </summary>
public class BindingSummaryTests
{
    private static CompilerDiagnostic At(int startColumn, int endColumn, string message) =>
        new("Error", message, 8, startColumn, 8, endColumn);

    private const string Summary =
        "Could not initialize binding '{value: X}', requirements … were not met.";

    [Fact]
    public void DropsTheSummaryWhenItShareTheRangeWithTheRealCause()
    {
        var issues = DiagnosticConversion.ToIssues(new[]
        {
            At(10, 20, "InvalidOperationException: cannot convert char[]"),
            At(10, 20, Summary),
        });

        Assert.StartsWith("InvalidOperationException", Assert.Single(issues).Message);
    }

    /// <summary>Alone it is all the user would get, so it stays.</summary>
    [Fact]
    public void KeepsTheSummaryWhenNothingElseCoversThatPlace()
    {
        var issues = DiagnosticConversion.ToIssues(new[] { At(10, 20, Summary) });

        Assert.Single(issues);
    }

    [Fact]
    public void KeepsTheSummaryWhenTheOtherFindingIsElsewhere()
    {
        var issues = DiagnosticConversion.ToIssues(new[]
        {
            At(40, 50, "a finding somewhere else"),
            At(10, 20, Summary),
        });

        Assert.Equal(2, issues.Count);
    }
}
