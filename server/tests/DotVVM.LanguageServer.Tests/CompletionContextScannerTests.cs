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
    /// An attribute counts as written wherever it sits on the tag. Collecting only what precedes
    /// the caret meant that stepping in front of Text offered Text again.
    /// </summary>
    [Fact]
    public void ReportsTheAttributesWrittenAfterTheCaretToo()
    {
        var context = At("<dot:Button |Text=\"x\" Click=\"y\" />");
        Assert.Equal(new[] { "Text", "Click" }, context.WrittenAttributes);
    }

    [Fact]
    public void ReportsTheAttributesOnBothSidesOfTheCaret()
    {
        var context = At("<dot:Button Text=\"x\" |Click=\"y\" />");
        Assert.Equal(new[] { "Text", "Click" }, context.WrittenAttributes);
    }

    /// <summary>
    /// The name the caret is inside is being replaced, not written - offering it back is the
    /// whole point of completing there.
    /// </summary>
    [Fact]
    public void DoesNotCountTheAttributeBeingEditedAsWritten()
    {
        var context = At("<dot:Button Te|xt=\"x\" Click=\"y\" />");
        Assert.Equal(new[] { "Click" }, context.WrittenAttributes);
    }

    /// <summary>
    /// A tag being typed has no '>' of its own, so the search for one runs into the next tag in
    /// the file. Its attributes must not be mistaken for this tag's.
    /// </summary>
    [Fact]
    public void DoesNotBorrowTheAttributesOfTheFollowingTag()
    {
        var context = At("<dot:Button |\n<dot:TextBox Text=\"a\" />");
        Assert.Equal(CompletionTarget.AttributeName, context.Target);
        Assert.Empty(context.WrittenAttributes!);
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

    /// <summary>
    /// Renaming an attribute that already has a value must not bring a second one along:
    /// completing over Enab|led="" used to produce Enabled=""="".
    /// </summary>
    [Fact]
    public void ReportsThatTheEditedAttributeAlreadyHasAValue()
    {
        Assert.True(At("<dot:Button Enab|led=\"\" />").EditedAttributeHasValue);
        Assert.True(At("<dot:Button Enab|led = \"x\" />").EditedAttributeHasValue);
    }

    [Fact]
    public void ReportsNoValueForAnAttributeNameOnItsOwn()
    {
        Assert.False(At("<dot:Button Ena| />").EditedAttributeHasValue);
    }

    [Fact]
    public void ReportsNoValueWhenNoAttributeIsBeingEdited()
    {
        Assert.False(At("<dot:Button |Text=\"x\" />").EditedAttributeHasValue);
        Assert.False(At("<dot:Button Text=\"x\" |/>").EditedAttributeHasValue);
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
