using DotVVM.LanguageServer.Model;
using Xunit;

namespace DotVVM.LanguageServer.Tests;

public class ProjectTypesTests
{
    [Fact]
    public void MergingKeepsTypesFromBothSides()
    {
        var a = new ProjectTypes(new[] { "App.AVm" }, new[] { "App" });
        var b = new ProjectTypes(new[] { "App.BVm" }, new[] { "App.Sub" });

        var merged = a.MergedWith(b);

        Assert.Equal(new[] { "App.AVm", "App.BVm" }, merged.ViewModels.OrderBy(x => x));
        Assert.Equal(new[] { "App", "App.Sub" }, merged.Namespaces.OrderBy(x => x));
    }

    [Fact]
    public void MergingDoesNotRepeatAType()
    {
        var a = new ProjectTypes(new[] { "App.Vm" }, new[] { "App" });

        Assert.Single(a.MergedWith(a).ViewModels);
        Assert.Single(a.MergedWith(a).Namespaces);
    }

    [Fact]
    public void EmptyMergesToTheOtherSide()
    {
        var a = new ProjectTypes(new[] { "App.Vm" }, new[] { "App" });

        Assert.Single(ProjectTypes.Empty.MergedWith(a).ViewModels);
        Assert.Single(a.MergedWith(ProjectTypes.Empty).ViewModels);
    }
}
