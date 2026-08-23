using DotVVM.LanguageServer.Analysis;
using Xunit;

namespace DotVVM.LanguageServer.Tests;

public class DirectiveScannerTests
{
    [Fact]
    public void FindsTheDirectivesInTheHeader()
    {
        var found = DirectiveScanner.Scan("@viewModel App.Vm\n@import App\n<html></html>");

        Assert.Equal(2, found.Count);
        Assert.Equal("viewModel", found[0].Name);
        Assert.Equal("App.Vm", found[0].Value);
        Assert.Equal(0, found[0].Line);
        Assert.Equal("import", found[1].Name);
        Assert.Equal(1, found[1].Line);
    }

    [Fact]
    public void StopsAtTheDocumentBody()
    {
        // Past the first tag no directive can follow, and an at sign in text is not one
        var found = DirectiveScanner.Scan("@viewModel A\n<html>@import B</html>");
        Assert.Single(found);
    }

    [Fact]
    public void ABlankLineDoesNotEndTheBlock()
    {
        Assert.Equal(2, DirectiveScanner.Scan("@viewModel A\n\n@import B\n<html></html>").Count);
    }

    [Fact]
    public void SurvivesAByteOrderMark()
    {
        // U+FEFF is not whitespace to .NET and every real file starts with one
        Assert.Single(DirectiveScanner.Scan("﻿@viewModel A\n<html></html>"));
    }

    [Fact]
    public void ReportsADirectiveWithNoValue()
    {
        var found = DirectiveScanner.Scan("@noWrapperTag\n<html></html>");

        Assert.Equal("noWrapperTag", found[0].Name);
        Assert.Equal("", found[0].Value);
    }

    [Fact]
    public void ReportsAnUnknownNameToo()
    {
        // The scanner does not judge, it reports; the verdict belongs to the validator
        Assert.Single(DirectiveScanner.Scan("@totalNonsense Foo\n<html></html>"));
    }

    [Fact]
    public void AnUnknownNameDoesNotEndTheBlock()
    {
        // Unlike the plugin's scanner, which may swallow it: here the validator has to see
        // both the typo and everything after it
        Assert.Equal(2, DirectiveScanner.Scan("@viewModle A\n@import B\n<html></html>").Count);
    }

    [Fact]
    public void PointsAtTheNameItself()
    {
        var found = DirectiveScanner.Scan("   @viewModel App.Vm\n<html></html>");

        Assert.Equal(3, found[0].Character);
        Assert.Equal("@viewModel".Length, found[0].Length);
    }

    [Fact]
    public void HandlesCarriageReturns()
    {
        var found = DirectiveScanner.Scan("@viewModel A\r\n@import B\r\n<html></html>");

        Assert.Equal(2, found.Count);
        Assert.Equal("A", found[0].Value);
    }

    [Fact]
    public void AnEmptyFileHoldsNoDirective()
    {
        Assert.Empty(DirectiveScanner.Scan(""));
        Assert.Empty(DirectiveScanner.Scan("   \n\n"));
    }
}
