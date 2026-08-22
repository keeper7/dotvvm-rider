using DotVVM.LanguageServer.Analysis;
using Xunit;

namespace DotVVM.LanguageServer.Tests;

public class BaseTypeDirectiveTests
{
    [Fact]
    public void ReadsTypeNameWithoutAssembly()
    {
        const string text = "@viewModel System.Object\n@baseType App.Controls.Widget, App\n<div />";
        Assert.Equal("App.Controls.Widget", BaseTypeDirective.Parse(text));
    }

    [Fact]
    public void ReadsTypeNameWhenAssemblyIsAbsent()
    {
        Assert.Equal("App.Widget", BaseTypeDirective.Parse("@baseType App.Widget\n<div />"));
    }

    [Fact]
    public void SkipsByteOrderMark()
    {
        // Real files carry one, and it once already broke the directive scanner
        Assert.Equal("App.Widget", BaseTypeDirective.Parse("﻿@baseType App.Widget\n<div />"));
    }

    [Fact]
    public void ReturnsNullWhenDirectiveIsMissing()
    {
        Assert.Null(BaseTypeDirective.Parse("@viewModel System.Object\n<div />"));
    }

    [Fact]
    public void StopsAtFirstTag()
    {
        Assert.Null(BaseTypeDirective.Parse("<div>\n@baseType App.Widget\n</div>"));
    }
}
