using DotVVM.LanguageServer.Analysis;
using Xunit;

namespace DotVVM.LanguageServer.Tests;

public class BindingContextScannerTests
{
    /// <summary>Detects at the caret marked with '|', which is removed before scanning.</summary>
    private static BindingContext At(string marked)
    {
        var caret = marked.IndexOf('|');
        var text = marked.Remove(caret, 1);

        var line = 0;
        var lineStart = 0;
        for (var i = 0; i < caret; i++)
        {
            if (text[i] != '\n') continue;
            line++;
            lineStart = i + 1;
        }

        return BindingContextScanner.Detect(text, line, caret - lineStart);
    }

    [Fact]
    public void TheOpeningBraceAloneAsksForAKind()
    {
        var context = At("<span>{|</span>");

        Assert.Equal(BindingTarget.BindingKind, context.Target);
        Assert.Equal("", context.Word);
    }

    [Fact]
    public void ReportsTheKindBeingTyped()
    {
        Assert.Equal("va", At("<span>{va|</span>").Word);
    }

    [Fact]
    public void TwoBracesAskForAKindAsWell()
    {
        Assert.Equal(BindingTarget.BindingKind, At("<span>{{|</span>").Target);
    }

    [Fact]
    public void TheCaretBetweenTheTwoBracesAsksForAKindToo()
    {
        // The second brace is not written yet as far as the author is concerned
        Assert.Equal(BindingTarget.BindingKind, At("<span>{|{</span>").Target);
    }

    [Fact]
    public void AfterTheColonTheDataContextIsWhatIsAskedFor()
    {
        var context = At("<span>{{value: |</span>");

        Assert.Equal(BindingTarget.Member, context.Target);
        Assert.Equal("value", context.Kind);
        Assert.Equal("", context.Path);
        Assert.Equal("", context.Word);
    }

    [Fact]
    public void ReportsTheWordBeingTyped()
    {
        var context = At("<span>{{value: Cust|");

        Assert.Equal("", context.Path);
        Assert.Equal("Cust", context.Word);
    }

    [Fact]
    public void ADotMakesWhatStandsBeforeItThePath()
    {
        var context = At("<span>{{value: Customer.|");

        Assert.Equal("Customer", context.Path);
        Assert.Equal("", context.Word);
    }

    [Fact]
    public void ThePathChains()
    {
        var context = At("<span>{{value: Customer.Address.Ci|");

        Assert.Equal("Customer.Address", context.Path);
        Assert.Equal("Ci", context.Word);
    }

    [Fact]
    public void AnIndexIsPartOfThePath()
    {
        Assert.Equal("Items[0]", At("<span>{{value: Items[0].|").Path);
    }

    [Fact]
    public void ACallIsPartOfThePath()
    {
        Assert.Equal("Name.ToUpper()", At("<span>{{value: Name.ToUpper().|").Path);
    }

    [Fact]
    public void ThePathStopsAtAnOpenCall()
    {
        // Inside the argument the receiver is the argument, not the method being called
        Assert.Equal("arg", At("<span>{{value: Method(arg.|").Path);
    }

    [Fact]
    public void ThePathStopsAtAnOperator()
    {
        var context = At("<span>{{value: Count + Customer.Ci|");

        Assert.Equal("Customer", context.Path);
        Assert.Equal("Ci", context.Word);
    }

    [Fact]
    public void TheKindsOwnColonIsTheFirstOne()
    {
        // A conditional carries a colon of its own, and it is not the one that ends the kind
        var context = At("<span>{{value: a ? b : c.|");

        Assert.Equal("value", context.Kind);
        Assert.Equal("c", context.Path);
    }

    [Fact]
    public void AnUnreadableReceiverOffersNothing()
    {
        // Offering the data context's own members after `"abc".` would answer another question
        Assert.Equal(BindingTarget.None, At("<span>{{value: \"abc\".Le|").Target);
    }

    [Fact]
    public void OutsideABindingThereIsNothingToComplete()
    {
        Assert.Equal(BindingTarget.None, At("<span>Hello |</span>").Target);
    }

    [Fact]
    public void ABindingThatEndedBeforeTheCaretIsNotTheCaretsOwn()
    {
        Assert.Equal(BindingTarget.None, At("<span>{{value: Name}} |</span>").Target);
    }

    [Fact]
    public void TheCaretBeforeTheClosingBracesIsStillInside()
    {
        var context = At("<span>{{value: Name|}}</span>");

        Assert.Equal(BindingTarget.Member, context.Target);
        Assert.Equal("Name", context.Word);
    }

    [Fact]
    public void ABindingMaySpanSeveralLines()
    {
        var context = At("<span>{{value: Customer\n    .Address.Ci|}}</span>");

        Assert.Equal("Customer\n    .Address", context.Path);
        Assert.Equal("Ci", context.Word);
    }

    [Fact]
    public void AnAttributeValueHoldsBindingsToo()
    {
        var context = At("<dot:Literal Text=\"{value: Cust|\"");

        Assert.Equal(BindingTarget.Member, context.Target);
        Assert.Equal("Cust", context.Word);
    }

    [Fact]
    public void OneBindingAfterAnotherInTheSameAttribute()
    {
        var context = At("<a Text=\"{value: A} {value: B.|\"");

        Assert.Equal("B", context.Path);
    }

    [Fact]
    public void AnUnknownKeywordIsNotABinding()
    {
        Assert.Equal(BindingTarget.None, At("<span>{foo: bar|").Target);
    }

    [Fact]
    public void BracesInAStyleBlockAreNotBindings()
    {
        // CSS punctuates with braces, and DotVVM does not read a style block at all
        Assert.Equal(BindingTarget.None, At("<style>.a { value: b|").Target);
    }

    [Fact]
    public void BracesInAScriptAreNotBindingsEither()
    {
        Assert.Equal(BindingTarget.None, At("<script>var o = {value: x|").Target);
    }

    [Fact]
    public void ABindingCommentedOutIsNotOne()
    {
        Assert.Equal(BindingTarget.None, At("<%-- {{value: Name|").Target);
    }

    [Fact]
    public void AScriptThatEndsLeavesTheBindingsAfterItAlone()
    {
        var context = At("<script>var a = {};</script><span>{{value: Na|");

        Assert.Equal(BindingTarget.Member, context.Target);
        Assert.Equal("Na", context.Word);
    }

    [Fact]
    public void AClosingBraceInsideAStringDoesNotEndTheBinding()
    {
        // The same rule the plugin's scanner follows, and the reason it is not a regex
        var context = At("<span>{{value: Format(\"{0}\", Name).To|}}");

        Assert.Equal(BindingTarget.Member, context.Target);
        Assert.Equal("To", context.Word);
    }
}
