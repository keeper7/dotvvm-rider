using DotVVM.LanguageServer.Analysis;
using DotVVM.LanguageServer.Model;
using Xunit;

namespace DotVVM.LanguageServer.Tests;

public class DirectiveValidatorTests
{
    private static IReadOnlyList<ValidationIssue> Validate(
        string text, string fileName = "Test.dothtml") =>
        DirectiveValidator.Validate(text, fileName, ControlRegistry.Empty);

    [Fact]
    public void RepeatedSingleValuedDirectiveIsAnError()
    {
        // DotVVM: "Directive 'viewModel' cannot be present multiple times."
        var issues = Validate("@viewModel A\n@viewModel B\n<html></html>");

        var issue = Assert.Single(issues);
        Assert.Equal(DiagnosticLevel.Error, issue.Level);
        Assert.Contains("viewModel", issue.Message);
        Assert.Equal(1, issue.Line);        // the second one is reported, not the first
    }

    [Fact]
    public void RepeatedImportIsFine()
    {
        // MarkupPageMetadata holds Imports as a list, so more than one is legal. Measured:
        // it repeats in 46 files of a real project.
        Assert.Empty(Validate("@viewModel A\n@import X\n@import Y\n<html></html>"));
    }

    [Fact]
    public void RepeatedServiceIsFineToo()
    {
        Assert.Empty(Validate("@viewModel A\n@service a = X\n@service b = Y\n<html></html>"));
    }

    [Fact]
    public void AnUnknownNameIsAnError()
    {
        // DotVVM passes it over in silence, so the user would never find out; it is almost
        // certainly a typo
        var issue = Assert.Single(Validate("@viewModel A\n@viewModle B\n<html></html>"));
        Assert.Contains("viewModle", issue.Message);
        Assert.Equal(DiagnosticLevel.Error, issue.Level);
    }

    [Fact]
    public void AMissingValueIsAnError()
    {
        // DotVVM: "Could not resolve type ''."
        Assert.Single(Validate("@viewModel\n<html></html>"));
    }

    [Fact]
    public void ADirectiveThatTakesNoValueMayHaveNone()
    {
        Assert.Empty(Validate("@viewModel A\n@noWrapperTag\n<html></html>", "Test.dotcontrol"));
    }

    [Fact]
    public void AValueOnNoWrapperTagIsAWarning()
    {
        // The framework takes it and throws it away — not an error, but it does nothing
        var issue = Assert.Single(Validate(
            "@viewModel A\n@noWrapperTag something\n<html></html>", "Test.dotcontrol"));
        Assert.Equal(DiagnosticLevel.Warning, issue.Level);
    }

    [Fact]
    public void ServiceWithoutAnAssignmentIsAnError()
    {
        // DotVVM: "Assignment operation expected - the correct form is
        // `@service myStringService = ISomeService<string>`"
        var issue = Assert.Single(Validate("@viewModel A\n@service System.String\n<html></html>"));
        Assert.Contains("=", issue.Message);
    }

    [Fact]
    public void AHealthyHeaderIsSilent()
    {
        Assert.Empty(Validate("@viewModel App.Vm, App\n@import App.Resources\n<html></html>"));
    }

    [Fact]
    public void TheIssuePointsAtTheDirectiveName()
    {
        var issue = Assert.Single(Validate("@viewModel A\n   @viewModel B\n<html></html>"));

        Assert.Equal(1, issue.Line);
        Assert.Equal(3, issue.Character);
        Assert.Equal("@viewModel".Length, issue.Length);
    }

    [Fact]
    public void AViewWithoutAViewModelIsAnError()
    {
        // DotVVM: "The @viewModel directive is missing in the page 'Test.dothtml'!"
        var issue = Assert.Single(Validate("<html></html>", "Test.dothtml"));

        Assert.Contains("@viewModel", issue.Message);
        Assert.Equal(0, issue.Line);        // there is nowhere else to point
    }

    [Fact]
    public void AMarkupControlNeedsItToo()
    {
        // Verified with the resolver: it holds for .dotcontrol as well, and all 67 markup
        // controls of the real project have one
        Assert.Single(Validate("@baseType App.C\n<html></html>", "Test.dotcontrol"));
    }

    [Fact]
    public void AMasterPageNeedsItAsWell()
    {
        Assert.Single(Validate("<html></html>", "Test.dotmaster"));
    }

    [Fact]
    public void AnEmptyFileIsNotWorthComplainingAbout()
    {
        // The user has only just created it; underlining its first line is nagging
        Assert.Empty(Validate("", "Test.dothtml"));
        Assert.Empty(Validate("   \n\n", "Test.dothtml"));
    }

    [Fact]
    public void AFileThatHasOneIsSilent()
    {
        Assert.Empty(Validate("@viewModel App.Vm\n<html></html>", "Test.dothtml"));
    }

    [Fact]
    public void AMarkupControlDirectiveInAViewIsAWarning()
    {
        // Verified with the resolver: the framework says nothing, but a wrapper tag governs
        // nothing in a view either
        var issue = Assert.Single(Validate(
            "@viewModel A\n@noWrapperTag\n<html></html>", "Test.dothtml"));

        Assert.Equal(DiagnosticLevel.Warning, issue.Level);
        Assert.Contains("noWrapperTag", issue.Message);
    }

    [Fact]
    public void TheSameDirectiveInAControlIsFine()
    {
        Assert.Empty(Validate("@viewModel A\n@noWrapperTag\n<html></html>", "Test.dotcontrol"));
    }

    [Fact]
    public void BaseTypeInAViewIsAnError()
    {
        // Here the framework does complain: "Markup controls must derive from
        // DotvvmMarkupControl class!"
        var issue = Assert.Single(Validate(
            "@viewModel A\n@baseType App.C\n<html></html>", "Test.dothtml"));

        Assert.Equal(DiagnosticLevel.Error, issue.Level);
    }

    [Fact]
    public void MasterPageInAControlIsAWarning()
    {
        var issue = Assert.Single(Validate(
            "@viewModel A\n@masterPage Views/Site.dotmaster\n<html></html>", "Test.dotcontrol"));

        Assert.Equal(DiagnosticLevel.Warning, issue.Level);
    }

    [Fact]
    public void MasterPageInAMasterIsFine()
    {
        // A master page may inherit from another; measured 26 times in the real project
        Assert.Empty(Validate(
            "@viewModel A\n@masterPage Views/Site.dotmaster\n<html></html>", "Test.dotmaster"));
    }

    [Fact]
    public void ADirectiveThatFitsAnywhereIsNeverFlagged()
    {
        foreach (var file in new[] { "A.dothtml", "A.dotmaster", "A.dotcontrol" })
            Assert.Empty(Validate("@viewModel A\n@import X\n@service a = B\n<html></html>", file));
    }
}
