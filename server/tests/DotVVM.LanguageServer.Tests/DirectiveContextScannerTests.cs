using DotVVM.LanguageServer.Analysis;
using Xunit;

namespace DotVVM.LanguageServer.Tests;

public class DirectiveContextScannerTests
{
    [Fact]
    public void DetectsTheValueOfAViewModelDirective()
    {
        var context = DirectiveContextScanner.Detect("@viewModel ", 0, 11);

        Assert.Equal("viewModel", context.Name);
        Assert.Equal("", context.WrittenValue);
    }

    [Fact]
    public void ReportsWhatIsAlreadyTyped()
    {
        var context = DirectiveContextScanner.Detect("@viewModel App.Vm", 0, 17);

        Assert.Equal("viewModel", context.Name);
        Assert.Equal("App.Vm", context.WrittenValue);
    }

    [Fact]
    public void TheNameItselfIsNotAValue()
    {
        // The plugin completes the names; the server must not join in
        Assert.Null(DirectiveContextScanner.Detect("@viewMo", 0, 7).Name);
    }

    [Fact]
    public void TheCaretOnTheNameIsNotAValueEither()
    {
        Assert.Null(DirectiveContextScanner.Detect("@viewModel App.Vm", 0, 6).Name);
    }

    [Fact]
    public void ReportsTheAssemblyPartAsSuch()
    {
        // After the comma comes the assembly, not a type: the directive is still the one being
        // written, but what belongs there is a different list
        var context = DirectiveContextScanner.Detect("@viewModel App.Vm, App", 0, 22);

        Assert.Equal("viewModel", context.Name);
        Assert.True(context.InAssembly);
        Assert.Equal("App", context.WrittenValue);
    }

    [Fact]
    public void TheTypePartIsNotTheAssemblyPart()
    {
        Assert.False(DirectiveContextScanner.Detect("@viewModel App.Vm", 0, 17).InAssembly);
    }

    [Fact]
    public void ReportsAnEmptyAssemblyRightAfterTheComma()
    {
        var context = DirectiveContextScanner.Detect("@viewModel App.Vm, ", 0, 19);

        Assert.True(context.InAssembly);
        Assert.Equal("", context.WrittenValue);
    }

    [Fact]
    public void ReadsPastABlankLine()
    {
        // A blank line between directives does not end the block
        var context = DirectiveContextScanner.Detect("@viewModel A\n\n@masterPage ", 2, 12);
        Assert.Equal("masterPage", context.Name);
    }

    [Fact]
    public void SaysNothingOnceTheBodyHasStarted()
    {
        Assert.Null(DirectiveContextScanner.Detect("<html>\n@viewModel ", 1, 11).Name);
    }

    [Fact]
    public void SurvivesAByteOrderMark()
    {
        // U+FEFF is not whitespace to .NET and every real file starts with one
        var context = DirectiveContextScanner.Detect("﻿@viewModel ", 0, 12);
        Assert.Equal("viewModel", context.Name);
    }

    [Fact]
    public void HandlesAnAliasOnTheLeftOfAService()
    {
        // @service alias = Type: what stands left of '=' is a name the user picks
        var context = DirectiveContextScanner.Detect("@service search = App.Svc", 0, 25);

        Assert.Equal("service", context.Name);
        Assert.Equal("App.Svc", context.WrittenValue);
    }

    [Fact]
    public void HandlesAnIndentedDirective()
    {
        var context = DirectiveContextScanner.Detect("   @masterPage Views/", 0, 21);

        Assert.Equal("masterPage", context.Name);
        Assert.Equal("Views/", context.WrittenValue);
    }

    [Fact]
    public void ReportsOnlyWhatStandsBeforeTheCaret()
    {
        // Completing over an existing value replaces what follows, so it is not a prefix
        var context = DirectiveContextScanner.Detect("@viewModel App.Vm", 0, 14);

        Assert.Equal("viewModel", context.Name);
        Assert.Equal("App", context.WrittenValue);
    }

    [Fact]
    public void SurvivesACaretPastTheEndOfTheLine()
    {
        // The editor can ask about a column that no longer exists after an edit
        var context = DirectiveContextScanner.Detect("@viewModel A", 0, 400);
        Assert.Equal("viewModel", context.Name);
    }
}
