using DotVVM.LanguageServer.Analysis;
using Xunit;

namespace DotVVM.LanguageServer.Tests;

public class BindingCompletionTests
{
    [Fact]
    public void OffersTheFourKindsAPageCanUse()
    {
        var labels = BindingCompletion.Kinds("/app/Views/Page.dothtml")
            .Select(s => s.Label).ToList();

        Assert.Equal(new[] { "value:", "command:", "staticCommand:", "resource:" }, labels);
    }

    [Fact]
    public void AMarkupControlCanBindToItsOwnProperties()
    {
        var labels = BindingCompletion.Kinds("/app/Controls/My.dotcontrol")
            .Select(s => s.Label).ToList();

        Assert.Contains("controlProperty:", labels);
        Assert.Contains("controlCommand:", labels);
    }

    [Fact]
    public void APageCannotBindToAControlProperty()
    {
        // In a page it compiles to "control property binding used outside a markup control"
        var labels = BindingCompletion.Kinds("/app/Views/Page.dothtml")
            .Select(s => s.Label).ToList();

        Assert.DoesNotContain("controlProperty:", labels);
    }

    [Fact]
    public void ValueSortsFirst()
    {
        var kinds = BindingCompletion.Kinds("/app/Views/Page.dothtml");

        Assert.Equal("value:", kinds.OrderBy(k => k.SortText, StringComparer.Ordinal).First().Label);
    }

    [Fact]
    public void TheColonIsInsertedWithTheKind()
    {
        // Typing the space after it is what opens the list of members, since a space is a
        // trigger character - so the colon has to be there already
        var value = BindingCompletion.Kinds("/app/Views/Page.dothtml").First(k => k.Label == "value:");

        Assert.Equal("value:", value.InsertText);
        Assert.False(value.IsSnippet);
    }
}
