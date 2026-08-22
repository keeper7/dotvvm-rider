using DotVVM.LanguageServer.Analysis;
using DotVVM.LanguageServer.Model;
using Xunit;

namespace DotVVM.LanguageServer.Tests;

public class ControlHoverTextTests
{
    private static ControlRegistration Namespaced(string prefix, string ns) =>
        new(prefix, ns, "SomeAssembly", null, null);

    private static ControlRegistration Markup(string prefix, string tag, string? baseType = null) =>
        new(prefix, null, null, tag, $"Controls/{tag}.dotcontrol", baseType);

    private static string Build(
        ControlRegistry registry, string prefix, string tag, bool knowsProjectPrefixes = true) =>
        ControlHoverText.Build(registry, knowsProjectPrefixes, prefix, tag);

    [Fact]
    public void ListsTheTypeAndItsProperties()
    {
        var registry = new ControlRegistry(
            new[] { Namespaced("dot", "DotVVM.Framework.Controls") },
            new[] { new ControlInfo("DotVVM.Framework.Controls.Repeater", null, "ItemTemplate",
                                    new[] { "DataSource", "Visible" }) });

        var text = Build(registry, "dot", "Repeater");

        Assert.Contains("**dot:Repeater**", text);
        Assert.Contains("`DotVVM.Framework.Controls.Repeater`", text);
        Assert.Contains("Default content property: `ItemTemplate`", text);
        Assert.Contains("Properties: `DataSource`, `Visible`", text);
    }

    [Fact]
    public void CountsThePropertiesItDidNotList()
    {
        var registry = new ControlRegistry(
            new[] { Namespaced("dot", "Ns") },
            new[] { new ControlInfo("Ns.Big", null, null,
                                    Enumerable.Range(0, 20).Select(i => $"P{i}").ToList()) });

        var text = Build(registry, "dot", "Big");

        Assert.Contains("`P14`", text);
        Assert.DoesNotContain("`P15`", text);
        Assert.Contains("and 5 more", text);
    }

    /// <summary>
    /// The message that started this: it called every unknown control a markup control, which
    /// for a typed one is simply wrong.
    /// </summary>
    [Fact]
    public void CallsAControlAMarkupControlOnlyWhenItIsOne()
    {
        var registry = new ControlRegistry(
            new[] { Markup("cc", "Widget"), Namespaced("dot", "DotVVM.Framework.Controls") },
            Array.Empty<ControlInfo>());

        Assert.Contains("Markup control", Build(registry, "cc", "Widget"));
        Assert.DoesNotContain("Markup control", Build(registry, "dot", "Repeater"));
    }

    [Fact]
    public void SaysAnUnknownTagWasNotFound()
    {
        var registry = new ControlRegistry(
            new[] { Namespaced("dot", "DotVVM.Framework.Controls") }, Array.Empty<ControlInfo>());

        Assert.Contains("Not found among the registered controls", Build(registry, "dot", "NoSuch"));
    }

    /// <summary>
    /// An unknown prefix and an unknown tag are different problems, and TagValidator already
    /// tells them apart. The tooltip must not say something other than the squiggle under it.
    /// </summary>
    [Fact]
    public void TellsAnUnknownPrefixFromAnUnknownTag()
    {
        var registry = new ControlRegistry(
            new[] { Namespaced("dot", "DotVVM.Framework.Controls") }, Array.Empty<ControlInfo>());

        Assert.Contains("Unknown control prefix 'zz'", Build(registry, "zz", "Whatever"));
        Assert.Contains("Not found among the registered controls", Build(registry, "dot", "NoSuch"));
    }

    /// <summary>
    /// A prefix the loaded source could not have seen is not evidence of anything - the same
    /// reason TagValidator stays silent there. Hover must not contradict it.
    /// </summary>
    [Fact]
    public void ClaimsNothingAboutAForeignPrefixWhenProjectPrefixesAreUnknown()
    {
        var registry = new ControlRegistry(
            new[] { Namespaced("dot", "DotVVM.Framework.Controls") }, Array.Empty<ControlInfo>());

        var text = Build(registry, "cc", "Widget", knowsProjectPrefixes: false);

        Assert.Equal("**cc:Widget**", text);
    }

    [Fact]
    public void ClaimsNothingWhenTheRegistryIsEmpty()
    {
        Assert.Equal("**dot:Repeater**", Build(ControlRegistry.Empty, "dot", "Repeater"));
    }

    /// <summary>
    /// A markup control whose base type resolved is an ordinary control from here on: it must
    /// report the code-behind class and its properties, not the apology.
    /// </summary>
    [Fact]
    public void ReportsAResolvedMarkupControlLikeAnyOther()
    {
        var registry = new ControlRegistry(
            new[] { Markup("cc", "Widget", baseType: "App.Controls.Widget") },
            new[] { new ControlInfo("App.Controls.Widget", null, null, new[] { "Data" }) });

        var text = Build(registry, "cc", "Widget");

        Assert.Contains("`App.Controls.Widget`", text);
        Assert.Contains("`Data`", text);
        Assert.DoesNotContain("not known", text);
    }

    /// <summary>
    /// A control whose type is known but which declares no properties: the type is reported and
    /// nothing is apologised for. Neither "markup control" nor "not found" would be true here.
    /// </summary>
    [Fact]
    public void ReportsAControlThatDeclaresNoProperties()
    {
        var registry = new ControlRegistry(
            new[] { Namespaced("dot", "Ns") },
            new[] { new ControlInfo("Ns.Button", null, null, Array.Empty<string>()) });

        var text = Build(registry, "dot", "Button");

        Assert.DoesNotContain("Markup control", text);
        Assert.DoesNotContain("Not found", text);
        Assert.DoesNotContain("Properties:", text);
        Assert.Contains("`Ns.Button`", text);
    }
}
