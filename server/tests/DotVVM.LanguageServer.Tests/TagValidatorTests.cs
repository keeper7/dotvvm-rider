using DotVVM.LanguageServer.Analysis;
using DotVVM.LanguageServer.Model;
using Xunit;

namespace DotVVM.LanguageServer.Tests;

public class TagValidatorTests
{
    private static ControlRegistry Registry => new(
        new[]
        {
            new ControlRegistration("dot", "DotVVM.Framework.Controls", "DotVVM.Framework", null, null),
            new ControlRegistration("cc", null, null, "Address", "Controls/Address.dotcontrol"),
        },
        new[]
        {
            new ControlInfo("DotVVM.Framework.Controls.Button", null, null,
                            new[] { new ControlProperty("Text") }),
        });

    [Fact]
    public void KnownTagProducesNoIssue()
    {
        Assert.Empty(TagValidator.Validate("<dot:Button Text=\"x\" />", Registry, knowsProjectPrefixes: true));
    }

    [Fact]
    public void UnknownPrefixIsReportedAsError()
    {
        var issues = TagValidator.Validate("<xyz:Thing />", Registry, knowsProjectPrefixes: true);
        var issue = Assert.Single(issues);
        Assert.Equal(DiagnosticLevel.Error, issue.Level);
        Assert.Contains("xyz", issue.Message);
    }

    [Fact]
    public void UnknownTagInKnownPrefixIsReportedAsError()
    {
        var issues = TagValidator.Validate("<dot:NoSuchControl />", Registry, knowsProjectPrefixes: true);
        var issue = Assert.Single(issues);
        Assert.Contains("NoSuchControl", issue.Message);
    }

    [Fact]
    public void MarkupControlIsAccepted()
    {
        Assert.Empty(TagValidator.Validate("<cc:Address />", Registry, knowsProjectPrefixes: true));
    }

    [Fact]
    public void PlainHtmlIsNotValidated()
    {
        Assert.Empty(TagValidator.Validate("<div><span>x</span></div>", Registry, knowsProjectPrefixes: true));
    }

    [Fact]
    public void IssueCarriesPositionOfTag()
    {
        var issues = TagValidator.Validate("<html>\n  <dot:Nope />\n</html>", Registry, knowsProjectPrefixes: true);
        var issue = Assert.Single(issues);
        Assert.Equal(1, issue.Line);
        Assert.Equal(3, issue.Character);
        Assert.Equal("dot:Nope".Length, issue.Length);
    }

    [Fact]
    public void UnknownPrefixIsSilentWhenProjectPrefixesAreUnknown()
    {
        // Tier 1 knows only the built-in controls, so it can claim nothing about the 'cc'
        // prefix. Underlining it would blame the user for something the server does not know.
        Assert.Empty(TagValidator.Validate("<cc:Address />", Registry, knowsProjectPrefixes: false));
    }

    [Fact]
    public void UnknownTagInKnownPrefixIsStillReportedWhenProjectPrefixesAreUnknown()
    {
        // Tier 1 knows the standard controls, so a typo in 'dot:' may still be reported.
        var issues = TagValidator.Validate("<dot:NoSuchControl />", Registry, knowsProjectPrefixes: false);
        Assert.Contains("NoSuchControl", Assert.Single(issues).Message);
    }

    [Fact]
    public void EmptyRegistryReportsNothing()
    {
        // without knowledge of the project the server must not flood the user with false errors
        Assert.Empty(TagValidator.Validate("<dot:Button />", ControlRegistry.Empty, knowsProjectPrefixes: true));
    }

    [Fact]
    public void ReportsEachUnknownTagSeparately()
    {
        var issues = TagValidator.Validate("<dot:A /><dot:B />", Registry, knowsProjectPrefixes: true);
        Assert.Equal(2, issues.Count);
    }

    [Fact]
    public void CommentedOutControlIsNotReported()
    {
        // What the user saw on a real project: a control switched off with <%-- --%> was
        // still underlined as unknown
        Assert.Empty(TagValidator.Validate(
            "<%-- <xyz:Thing /> --%>", Registry, knowsProjectPrefixes: true));
    }
}
