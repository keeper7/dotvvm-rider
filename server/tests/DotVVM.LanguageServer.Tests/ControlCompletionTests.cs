using DotVVM.LanguageServer.Analysis;
using DotVVM.LanguageServer.Model;
using Xunit;

namespace DotVVM.LanguageServer.Tests;

public class ControlCompletionTests
{
    private static ControlRegistry Registry() => new(
        new[] { new ControlRegistration("dot", "DotVVM.Framework.Controls", "DotVVM.Framework", null, null) },
        new[]
        {
            new ControlInfo("DotVVM.Framework.Controls.Repeater", null, null, new[]
            {
                new ControlProperty("DataSource", Value: PropertyValue.BindingOnly),
                new ControlProperty("ItemTemplate", PropertyUsage.InnerElement, Required: true),
                new ControlProperty("WrapperTagName", Value: PropertyValue.HardCodedOnly),
                new ControlProperty("Visible"),
            })
        },
        new[] { new ControlProperty("Validation.Enabled") });

    private static IReadOnlyList<CompletionSuggestion> For(CompletionContext context) =>
        ControlCompletion.Suggest(Registry(), context);

    private static CompletionContext InRepeater(params string[] written) =>
        new(CompletionTarget.AttributeName, "dot", "Repeater", written);

    [Fact]
    public void OffersThePropertiesOfTheControl()
    {
        Assert.Contains(For(InRepeater()), s => s.Label == "DataSource");
    }

    /// <summary>
    /// ItemTemplate is written as a child element. Offering it as an attribute would produce
    /// markup that does not compile - 44 of the framework's properties are like this.
    /// </summary>
    [Fact]
    public void LeavesOutThePropertiesThatAreNotAttributes()
    {
        Assert.DoesNotContain(For(InRepeater()), s => s.Label == "ItemTemplate");
    }

    [Fact]
    public void OffersABindingForAPropertyThatTakesNothingElse()
    {
        var item = For(InRepeater()).Single(s => s.Label == "DataSource");
        Assert.Equal("DataSource=\"{value: $0}\"", item.InsertText);
        Assert.True(item.IsSnippet);
    }

    [Fact]
    public void OffersAPlainValueOtherwise()
    {
        var item = For(InRepeater()).Single(s => s.Label == "WrapperTagName");
        Assert.Equal("WrapperTagName=\"$0\"", item.InsertText);
    }

    [Fact]
    public void LeavesOutThePropertiesAlreadyWritten()
    {
        Assert.DoesNotContain(For(InRepeater("Visible")), s => s.Label == "Visible");
    }

    [Fact]
    public void OffersTheAttachedPropertiesToo()
    {
        Assert.Contains(For(InRepeater()), s => s.Label == "Validation.Enabled");
    }

    /// <summary>691 of the 1428 attached uses in the real project sit on plain HTML elements.</summary>
    [Fact]
    public void OffersAttachedPropertiesOnAPlainHtmlElement()
    {
        var items = For(new CompletionContext(CompletionTarget.AttributeName, null, "div",
                                              Array.Empty<string>()));

        Assert.Contains(items, s => s.Label == "Validation.Enabled");
        Assert.DoesNotContain(items, s => s.Label == "DataSource");
    }

    [Fact]
    public void OffersTheRequiredPropertiesFirst()
    {
        var registry = new ControlRegistry(
            new[] { new ControlRegistration("dot", "Ns", "A", null, null) },
            new[] { new ControlInfo("Ns.Box", null, null, new[]
            {
                new ControlProperty("Zebra"),
                new ControlProperty("Alpha", Required: true),
            })});

        var items = ControlCompletion.Suggest(registry,
            new CompletionContext(CompletionTarget.AttributeName, "dot", "Box", Array.Empty<string>()));

        Assert.Equal("Alpha", items.OrderBy(i => i.SortText, StringComparer.Ordinal).First().Label);
    }

    /// <summary>
    /// Today the handler falls back to the prefixes whenever it cannot find a tag, so they pop
    /// up in the middle of text. Nothing is the right answer there.
    /// </summary>
    [Fact]
    public void OffersNothingWhereNothingBelongs()
    {
        Assert.Empty(For(CompletionContext.None));
    }

    [Fact]
    public void OffersPrefixesOnlyRightAfterTheAngleBracket()
    {
        var items = For(new CompletionContext(CompletionTarget.TagPrefix));
        Assert.Contains(items, s => s.Label == "dot");
    }

    [Fact]
    public void OffersTheTagsOfThePrefix()
    {
        var items = For(new CompletionContext(CompletionTarget.TagName, "dot"));
        Assert.Contains(items, s => s.Label == "Repeater");
    }

    [Fact]
    public void OffersNothingForAnUnknownPrefix()
    {
        Assert.Empty(For(new CompletionContext(CompletionTarget.TagName, "zz")));
    }

    /// <summary>
    /// An unknown tag has no properties, but the attached ones are written on anything - and the
    /// user is most likely typing into a control the registry does not know yet.
    /// </summary>
    [Fact]
    public void StillOffersAttachedPropertiesInAnUnknownTag()
    {
        var items = For(new CompletionContext(CompletionTarget.AttributeName, "cc", "NoSuch",
                                              Array.Empty<string>()));

        Assert.Contains(items, s => s.Label == "Validation.Enabled");
    }
}
