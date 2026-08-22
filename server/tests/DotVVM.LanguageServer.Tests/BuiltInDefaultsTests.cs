using DotVVM.LanguageServer.Configuration;
using Xunit;

namespace DotVVM.LanguageServer.Tests;

public class BuiltInDefaultsTests
{
    [Fact]
    public async Task ProvidesDotPrefix()
    {
        var registry = await new BuiltInDefaults().LoadAsync("/nonexistent", default);
        Assert.NotNull(registry);
        Assert.True(registry!.IsKnownPrefix("dot"));
    }

    [Fact]
    public async Task KnowsCommonControls()
    {
        var registry = await new BuiltInDefaults().LoadAsync("/nonexistent", default);
        foreach (var tag in new[] { "Button", "TextBox", "Repeater", "GridView", "RouteLink" })
        {
            Assert.True(registry!.IsKnownTag("dot", tag), $"missing control {tag}");
        }
    }

    [Fact]
    public async Task ButtonHasTextProperty()
    {
        var registry = await new BuiltInDefaults().LoadAsync("/nonexistent", default);
        var button = registry!.GetControl("dot", "Button");
        Assert.NotNull(button);
        Assert.Contains(button!.Properties, p => p.Name == "Text");
    }

    [Fact]
    public async Task DoesNotKnowCustomPrefix()
    {
        var registry = await new BuiltInDefaults().LoadAsync("/nonexistent", default);
        Assert.False(registry!.IsKnownPrefix("cc"));
    }
}
