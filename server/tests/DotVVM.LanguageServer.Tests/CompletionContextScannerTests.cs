using DotVVM.LanguageServer.Analysis;
using Xunit;

namespace DotVVM.LanguageServer.Tests;

public class CompletionContextScannerTests
{
    /// <summary>Runs the scanner with the caret written as '|' in the text.</summary>
    private static CompletionContext At(string textWithCaret)
    {
        var index = textWithCaret.IndexOf('|');
        Assert.True(index >= 0, "the test text must contain the caret marker '|'");

        var text = textWithCaret.Remove(index, 1);
        var before = text[..index];
        var line = before.Count(c => c == '\n');
        var lastBreak = before.LastIndexOf('\n');

        return CompletionContextScanner.Detect(text, line, index - (lastBreak + 1));
    }

    [Fact]
    public void OffersPrefixesRightAfterTheAngleBracket()
    {
        Assert.Equal(CompletionTarget.TagPrefix, At("<div><|</div>").Target);
    }

    [Fact]
    public void OffersTagNamesAfterThePrefixAndColon()
    {
        var context = At("<div><cc:|</div>");
        Assert.Equal(CompletionTarget.TagName, context.Target);
        Assert.Equal("cc", context.Prefix);
    }

    [Fact]
    public void OffersTagNamesWhileTheNameIsBeingTyped()
    {
        var context = At("<cc:Wid|");
        Assert.Equal(CompletionTarget.TagName, context.Target);
        Assert.Equal("cc", context.Prefix);
    }

    [Fact]
    public void OffersAttributesInsideAnOpenTag()
    {
        var context = At("<dot:Button |/>");
        Assert.Equal(CompletionTarget.AttributeName, context.Target);
        Assert.Equal("dot", context.Prefix);
        Assert.Equal("Button", context.TagName);
    }

    [Fact]
    public void OffersAttributesWhileTheAttributeNameIsBeingTyped()
    {
        Assert.Equal(CompletionTarget.AttributeName, At("<dot:Button Cli|/>").Target);
    }

    /// <summary>31 % of the prefixed tags in the real project span more than one line.</summary>
    [Fact]
    public void OffersAttributesOnALaterLineOfTheSameTag()
    {
        var context = At("<dot:Button class=\"x\"\n            Click=\"{command: A()}\"\n            |/>");
        Assert.Equal(CompletionTarget.AttributeName, context.Target);
        Assert.Equal("Button", context.TagName);
    }

    [Fact]
    public void ReportsTheAttributesAlreadyWritten()
    {
        var context = At("<dot:Button Text=\"x\" Enabled=\"true\" |/>");
        Assert.Equal(new[] { "Text", "Enabled" }, context.WrittenAttributes);
    }

    /// <summary>
    /// The half-typed attribute is not "already written" - filtering it out would remove the
    /// very item the user is reaching for.
    /// </summary>
    [Fact]
    public void DoesNotCountTheAttributeBeingTypedAsWritten()
    {
        var context = At("<dot:Button Text=\"x\" Ena|");
        Assert.Equal(new[] { "Text" }, context.WrittenAttributes);
    }

    [Fact]
    public void StaysSilentInsideAnAttributeValue()
    {
        Assert.Equal(CompletionTarget.None, At("<dot:Button Text=\"a|b\" />").Target);
    }

    [Fact]
    public void StaysSilentInsideABindingHoldingAngleBrackets()
    {
        Assert.Equal(CompletionTarget.None, At("<dot:Button Text=\"{value: A < B |}\" />").Target);
    }

    [Fact]
    public void StaysSilentInPlainText()
    {
        Assert.Equal(CompletionTarget.None, At("<div>hello |world</div>").Target);
    }

    [Fact]
    public void StaysSilentInsideAnHtmlComment()
    {
        Assert.Equal(CompletionTarget.None, At("<!-- <dot:Button | -->").Target);
    }

    /// <summary>The server-side comment; plan 5 teaches DothtmlScanner the same thing.</summary>
    [Fact]
    public void StaysSilentInsideAServerSideComment()
    {
        Assert.Equal(CompletionTarget.None, At("<%-- <dot:Button | --%>").Target);
    }

    [Fact]
    public void StaysSilentAfterTheTagIsClosed()
    {
        Assert.Equal(CompletionTarget.None, At("<dot:Button /> |").Target);
    }

    /// <summary>
    /// Attached properties are written on plain HTML elements too - Validation.Enabled appears
    /// 691 times in the real project. The element has no prefix, and that is not an error.
    /// </summary>
    [Fact]
    public void OffersAttributesOnAPlainHtmlElement()
    {
        var context = At("<div |>");
        Assert.Equal(CompletionTarget.AttributeName, context.Target);
        Assert.Null(context.Prefix);
        Assert.Equal("div", context.TagName);
    }

    [Fact]
    public void StaysSilentInsideAClosingTag()
    {
        Assert.Equal(CompletionTarget.None, At("<div></di|v>").Target);
    }
}
