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
            new ControlInfo("DotVVM.Framework.Controls.Button", null, null, new[] { "Text" }),
        });

    [Fact]
    public void KnownTagProducesNoIssue()
    {
        Assert.Empty(TagValidator.Validate("<dot:Button Text=\"x\" />", Registry));
    }

    [Fact]
    public void UnknownPrefixIsReportedAsError()
    {
        var issues = TagValidator.Validate("<xyz:Thing />", Registry);
        var issue = Assert.Single(issues);
        Assert.Equal(DiagnosticLevel.Error, issue.Level);
        Assert.Contains("xyz", issue.Message);
    }

    [Fact]
    public void UnknownTagInKnownPrefixIsReportedAsError()
    {
        var issues = TagValidator.Validate("<dot:NoSuchControl />", Registry);
        var issue = Assert.Single(issues);
        Assert.Contains("NoSuchControl", issue.Message);
    }

    [Fact]
    public void MarkupControlIsAccepted()
    {
        Assert.Empty(TagValidator.Validate("<cc:Address />", Registry));
    }

    [Fact]
    public void PlainHtmlIsNotValidated()
    {
        Assert.Empty(TagValidator.Validate("<div><span>x</span></div>", Registry));
    }

    [Fact]
    public void IssueCarriesPositionOfTag()
    {
        var issues = TagValidator.Validate("<html>\n  <dot:Nope />\n</html>", Registry);
        var issue = Assert.Single(issues);
        Assert.Equal(1, issue.Line);
        Assert.Equal(3, issue.Character);
        Assert.Equal("dot:Nope".Length, issue.Length);
    }

    [Fact]
    public void EmptyRegistryReportsNothing()
    {
        // bez znalosti projektu nesmí server zaplavit uživatele falešnými chybami
        Assert.Empty(TagValidator.Validate("<dot:Button />", ControlRegistry.Empty));
    }

    [Fact]
    public void ReportsEachUnknownTagSeparately()
    {
        var issues = TagValidator.Validate("<dot:A /><dot:B />", Registry);
        Assert.Equal(2, issues.Count);
    }
}
