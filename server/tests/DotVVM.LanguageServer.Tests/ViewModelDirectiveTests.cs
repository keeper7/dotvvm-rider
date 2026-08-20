using DotVVM.LanguageServer.Analysis;
using Xunit;

namespace DotVVM.LanguageServer.Tests;

public class ViewModelDirectiveTests
{
    [Fact]
    public void ParsesTypeAndAssembly()
    {
        var r = ViewModelDirective.Parse("@viewModel MyApp.ViewModels.FooViewModel, MyApp\n<html/>");
        Assert.NotNull(r);
        Assert.Equal("MyApp.ViewModels.FooViewModel", r!.TypeName);
        Assert.Equal("MyApp", r.AssemblyName);
        Assert.Equal(0, r.Line);
    }

    [Fact]
    public void ParsesTypeWithoutAssembly()
    {
        var r = ViewModelDirective.Parse("@viewModel MyApp.ViewModels.FooViewModel");
        Assert.NotNull(r);
        Assert.Equal("MyApp.ViewModels.FooViewModel", r!.TypeName);
        Assert.Null(r.AssemblyName);
    }

    [Fact]
    public void ReturnsNullWhenDirectiveMissing()
    {
        Assert.Null(ViewModelDirective.Parse("<html><body/></html>"));
    }

    [Fact]
    public void FindsDirectiveAfterOtherDirectives()
    {
        var text = "@masterPage Views/Site.dotmaster\n@viewModel App.VM, App\n<html/>";
        var r = ViewModelDirective.Parse(text);
        Assert.NotNull(r);
        Assert.Equal("App.VM", r!.TypeName);
        Assert.Equal(1, r.Line);
    }

    [Fact]
    public void ReportsRangeCoveringTypeNameOnly()
    {
        var r = ViewModelDirective.Parse("@viewModel App.VM, App");
        Assert.NotNull(r);
        Assert.Equal("@viewModel ".Length, r!.Character);
        Assert.Equal("App.VM".Length, r.Length);
    }

    [Fact]
    public void IgnoresCaseOfDirectiveName()
    {
        Assert.NotNull(ViewModelDirective.Parse("@viewmodel App.VM"));
    }

    [Fact]
    public void HandlesGenericViewModelType()
    {
        var r = ViewModelDirective.Parse("@viewModel App.ListViewModel<App.Item>, App");
        Assert.NotNull(r);
        Assert.Equal("App.ListViewModel<App.Item>", r!.TypeName);
    }
}
