using DotVVM.LanguageServer.Analysis;
using DotVVM.LanguageServer.Model;
using Xunit;

namespace DotVVM.LanguageServer.Tests;

public class CommentInTagTests
{
    private static ControlRegistry Registry => new(
        new[] { new ControlRegistration("dot", "DotVVM.Framework.Controls", "DotVVM.Framework", null, null) },
        new[]
        {
            new ControlInfo("DotVVM.Framework.Controls.TextBox", null, null,
                new[] { new ControlProperty("Text"), new ControlProperty("Enabled") }),
        });

    [Fact]
    public void TagWithACommentBetweenAttributesIsStillFound()
    {
        var tags = DothtmlScanner.ScanTags("<dot:TextBox <%-- Text=\"x\" --%> Enabled=\"true\" />");

        var tag = Assert.Single(tags);
        Assert.Equal("TextBox", tag.TagName);
    }

    [Fact]
    public void ACommentedOutAttributeIsOfferedAgain()
    {
        // The property is commented out, so it is not written on the tag and completion
        // must still offer it
        var text = "<dot:TextBox <%-- Text=\"x\" --%> ";
        var context = CompletionContextScanner.Detect(text, 0, text.Length);

        Assert.Equal(CompletionTarget.AttributeName, context.Target);
        Assert.DoesNotContain("Text", context.WrittenAttributes ?? new List<string>());
    }

    [Fact]
    public void AttributesAfterACommentStillCount()
    {
        var text = "<dot:TextBox <%-- a --%> Enabled=\"true\" ";
        var context = CompletionContextScanner.Detect(text, 0, text.Length);

        Assert.Equal(CompletionTarget.AttributeName, context.Target);
        Assert.Contains("Enabled", context.WrittenAttributes ?? new List<string>());
    }

    [Fact]
    public void NothingIsOfferedInsideTheCommentItself()
    {
        var text = "<dot:TextBox <%-- Te";
        var context = CompletionContextScanner.Detect(text, 0, text.Length);

        Assert.Equal(CompletionTarget.None, context.Target);
    }

    [Fact]
    public void ClosingBracketInsideACommentDoesNotEndTheTag()
    {
        // Without skipping the comment, EndOfTag would stop at the '>' inside it and the
        // attribute after it would be lost
        var text = "<dot:TextBox <%-- a > b --%> Enabled=\"true\" ";
        var context = CompletionContextScanner.Detect(text, 0, text.Length);

        Assert.Equal(CompletionTarget.AttributeName, context.Target);
        Assert.Contains("Enabled", context.WrittenAttributes ?? new List<string>());
    }

    [Fact]
    public void ValidationIsSilentInsideACommentedTag()
    {
        var registry = new ControlRegistry(
            new[] { new ControlRegistration("dot", "DotVVM.Framework.Controls", "DotVVM.Framework", null, null) },
            new[] { new ControlInfo("DotVVM.Framework.Controls.TextBox", null, null, new ControlProperty[0]) });

        Assert.Empty(TagValidator.Validate(
            "<dot:TextBox <%-- <xyz:Gone /> --%> />", registry, knowsProjectPrefixes: true));
    }
}
