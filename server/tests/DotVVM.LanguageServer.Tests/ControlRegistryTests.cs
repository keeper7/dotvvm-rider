using DotVVM.LanguageServer.Model;
using Xunit;

namespace DotVVM.LanguageServer.Tests;

public class ControlRegistryTests
{
    private static ControlRegistry BuildRegistry() => new(
        registrations: new[]
        {
            new ControlRegistration("dot", "DotVVM.Framework.Controls", "DotVVM.Framework", null, null),
            new ControlRegistration("cc", "MyApp.Controls", "MyApp", null, null),
            new ControlRegistration("cc", null, null, "Address", "Controls/Address.dotcontrol"),
        },
        controls: new[]
        {
            new ControlInfo("DotVVM.Framework.Controls.Button", "DotvvmControl", null,
                            new[] { "Text", "Click", "Enabled" }),
            new ControlInfo("MyApp.Controls.Widget", "DotvvmControl", "ContentTemplate",
                            new[] { "Value" }),
        });

    [Fact]
    public void KnownPrefixIsRecognized()
    {
        Assert.True(BuildRegistry().IsKnownPrefix("dot"));
        Assert.True(BuildRegistry().IsKnownPrefix("cc"));
    }

    [Fact]
    public void UnknownPrefixIsRejected()
    {
        Assert.False(BuildRegistry().IsKnownPrefix("xyz"));
    }

    [Fact]
    public void TagFromNamespaceRegistrationIsKnown()
    {
        Assert.True(BuildRegistry().IsKnownTag("dot", "Button"));
    }

    [Fact]
    public void UnknownTagInKnownPrefixIsRejected()
    {
        Assert.False(BuildRegistry().IsKnownTag("dot", "NoSuchControl"));
    }

    [Fact]
    public void MarkupControlRegisteredBySrcIsKnown()
    {
        Assert.True(BuildRegistry().IsKnownTag("cc", "Address"));
    }

    [Fact]
    public void GetTagsForPrefixListsBothKinds()
    {
        var tags = BuildRegistry().GetTagsForPrefix("cc");
        Assert.Contains("Widget", tags);
        Assert.Contains("Address", tags);
    }

    [Fact]
    public void GetControlReturnsPropertiesForKnownTag()
    {
        var control = BuildRegistry().GetControl("dot", "Button");
        Assert.NotNull(control);
        Assert.Contains("Text", control!.Properties);
    }

    [Fact]
    public void GetControlReturnsNullForMarkupControl()
    {
        // markup kontrolka nemá typ v seznamu controls — vlastnosti neznáme
        Assert.Null(BuildRegistry().GetControl("cc", "Address"));
    }

    [Fact]
    public void AllPrefixesAreDistinct()
    {
        Assert.Equal(new[] { "cc", "dot" }, BuildRegistry().AllPrefixes.OrderBy(p => p).ToArray());
    }
}
