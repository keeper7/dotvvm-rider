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
                            new[] { new ControlProperty("Text"), new ControlProperty("Click"),
                                    new ControlProperty("Enabled") }),
            new ControlInfo("MyApp.Controls.Widget", "DotvvmControl", "ContentTemplate",
                            new[] { new ControlProperty("Value") }),
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
        Assert.Contains(control!.Properties, p => p.Name == "Text");
    }

    [Fact]
    public void GetControlReturnsNullForMarkupControl()
    {
        // a markup control has no type in the controls list, so its properties are unknown
        Assert.Null(BuildRegistry().GetControl("cc", "Address"));
    }

    [Fact]
    public void AllPrefixesAreDistinct()
    {
        Assert.Equal(new[] { "cc", "dot" }, BuildRegistry().AllPrefixes.OrderBy(p => p).ToArray());
    }

    /// <summary>
    /// A markup control has no namespace, so only the resolved base type can lead to its
    /// properties. Without BaseTypeName the lookup must stay silent rather than guess by name.
    /// </summary>
    [Fact]
    public void FindsAMarkupControlThroughItsResolvedBaseType()
    {
        var controls = new[]
        {
            new ControlInfo("App.Controls.Widget", null, null, new[] { new ControlProperty("Data") })
        };

        var unresolved = new ControlRegistry(
            new[] { new ControlRegistration("cc", null, null, "Widget", "Controls/Widget.dotcontrol") },
            controls);
        Assert.Null(unresolved.GetControl("cc", "Widget"));

        var resolved = new ControlRegistry(
            new[] { new ControlRegistration("cc", null, null, "Widget", "Controls/Widget.dotcontrol",
                                            BaseTypeName: "App.Controls.Widget") },
            controls);
        Assert.Equal("App.Controls.Widget", resolved.GetControl("cc", "Widget")?.FullTypeName);
    }

    /// <summary>
    /// An attached property belongs to no control - it is written as Owner.Name on anything,
    /// including a plain HTML element. The registry keeps them apart for that reason.
    /// </summary>
    [Fact]
    public void KeepsAttachedPropertiesApartFromTheControls()
    {
        var registry = new ControlRegistry(
            new[] { new ControlRegistration("dot", "DotVVM.Framework.Controls", "DotVVM.Framework", null, null) },
            new[] { new ControlInfo("DotVVM.Framework.Controls.Button", null, null,
                                    new[] { new ControlProperty("Text") }) },
            new[] { new ControlProperty("Validation.Enabled") });

        Assert.Contains(registry.AttachedProperties, p => p.Name == "Validation.Enabled");
        Assert.DoesNotContain(registry.GetControl("dot", "Button")!.Properties,
                              p => p.Name == "Validation.Enabled");
    }

    [Fact]
    public void MergesAttachedPropertiesFromBothRegistries()
    {
        var lower = new ControlRegistry(
            Array.Empty<ControlRegistration>(), Array.Empty<ControlInfo>(),
            new[] { new ControlProperty("Validation.Enabled") });
        var higher = new ControlRegistry(
            Array.Empty<ControlRegistration>(), Array.Empty<ControlInfo>(),
            new[] { new ControlProperty("Validator.Value") });

        var merged = lower.MergedWith(higher);

        Assert.Equal(2, merged.AttachedProperties.Count);
    }
}
