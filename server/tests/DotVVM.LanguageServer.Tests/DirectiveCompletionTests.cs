using DotVVM.LanguageServer.Analysis;
using DotVVM.LanguageServer.Model;
using Xunit;

namespace DotVVM.LanguageServer.Tests;

public class DirectiveCompletionTests
{
    private static ControlRegistry Registry => new(
        new[] { new ControlRegistration("cc", "App.Controls", "App", null, null) },
        new[] { new ControlInfo("App.Controls.Card", null, null, Array.Empty<ControlProperty>()) },
        null,
        new ProjectTypes(
            new[] { "App.ViewModels.HomeViewModel", "App.ViewModels.ListViewModel" },
            new[] { "App", "App.Controls" }));

    [Fact]
    public void OffersViewModelsForTheViewModelDirective()
    {
        var items = DirectiveCompletion.Suggest(Registry, new DirectiveContext("viewModel", ""));

        Assert.Contains(items, i => i.Label == "App.ViewModels.HomeViewModel");
        Assert.Contains(items, i => i.Label == "App.ViewModels.ListViewModel");
    }

    [Fact]
    public void OffersControlTypesForBaseType()
    {
        // @baseType names a control, not a view model — offering the latter would be worse
        // than offering nothing
        var items = DirectiveCompletion.Suggest(Registry, new DirectiveContext("baseType", ""));

        Assert.Contains(items, i => i.Label == "App.Controls.Card");
        Assert.DoesNotContain(items, i => i.Label.Contains("ViewModel"));
    }

    [Fact]
    public void OffersNamespacesForImport()
    {
        var items = DirectiveCompletion.Suggest(Registry, new DirectiveContext("import", ""));

        Assert.Contains(items, i => i.Label == "App.Controls");
        Assert.DoesNotContain(items, i => i.Label.Contains("ViewModel"));
    }

    [Fact]
    public void OffersNamespacesForResourceNamespace()
    {
        var items = DirectiveCompletion.Suggest(Registry, new DirectiveContext("resourceNamespace", ""));
        Assert.Contains(items, i => i.Label == "App");
    }

    [Fact]
    public void SaysNothingForADirectiveWithoutAValue()
    {
        Assert.Empty(DirectiveCompletion.Suggest(Registry, new DirectiveContext("noWrapperTag", "")));
    }

    [Fact]
    public void SaysNothingForADirectiveItCannotServe()
    {
        // @service and @resourceType name any type of the project, which the registry does not
        // hold. Two occurrences in a real project of 244 views — deliberately left empty.
        Assert.Empty(DirectiveCompletion.Suggest(Registry, new DirectiveContext("service", "")));
        Assert.Empty(DirectiveCompletion.Suggest(Registry, new DirectiveContext("resourceType", "")));
    }

    [Fact]
    public void SaysNothingWithAnEmptyRegistry()
    {
        // Knowing nothing about the project, silence beats invention — the rule the validator
        // follows too
        Assert.Empty(DirectiveCompletion.Suggest(
            ControlRegistry.Empty, new DirectiveContext("viewModel", "")));
    }

    [Fact]
    public void SaysNothingWhenTheCaretIsNotInADirective()
    {
        Assert.Empty(DirectiveCompletion.Suggest(Registry, DirectiveContext.None));
    }

    [Fact]
    public void ShortestNamespaceComesFirst()
    {
        // A namespace is most often the outermost one; sorting by length puts it on top
        var items = DirectiveCompletion.Suggest(Registry, new DirectiveContext("import", ""));
        Assert.Equal("App", items.OrderBy(i => i.SortText).First().Label);
    }
}
