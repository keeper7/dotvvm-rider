using DotVVM.LanguageServer.Analysis;
using Xunit;

namespace DotVVM.LanguageServer.Tests;

public class DothtmlScannerTests
{
    [Fact]
    public void FindsPrefixedTag()
    {
        var tags = DothtmlScanner.ScanTags("<dot:Button Text=\"x\" />");
        var tag = Assert.Single(tags);
        Assert.Equal("dot", tag.Prefix);
        Assert.Equal("Button", tag.TagName);
        Assert.Equal(0, tag.Line);
        Assert.Equal(1, tag.Character);          // just past the '<'
        Assert.Equal("dot:Button".Length, tag.Length);
    }

    [Fact]
    public void IgnoresPlainHtmlTags()
    {
        Assert.Empty(DothtmlScanner.ScanTags("<div class=\"a\"><span>x</span></div>"));
    }

    [Fact]
    public void IgnoresClosingTags()
    {
        var tags = DothtmlScanner.ScanTags("<dot:Repeater></dot:Repeater>");
        Assert.Single(tags);
    }

    [Fact]
    public void ReportsCorrectLineForTagOnSecondLine()
    {
        var tags = DothtmlScanner.ScanTags("<html>\n  <dot:Button />\n</html>");
        var tag = Assert.Single(tags);
        Assert.Equal(1, tag.Line);
        Assert.Equal(3, tag.Character);
    }

    [Fact]
    public void FindsMultipleTags()
    {
        var tags = DothtmlScanner.ScanTags("<dot:Button /><cc:Widget />");
        Assert.Equal(2, tags.Count);
        Assert.Equal("cc", tags[1].Prefix);
    }

    [Fact]
    public void IgnoresTagInsideComment()
    {
        Assert.Empty(DothtmlScanner.ScanTags("<!-- <dot:Button /> -->"));
    }

    [Fact]
    public void IgnoresXmlDeclarationAndDoctype()
    {
        Assert.Empty(DothtmlScanner.ScanTags("<!DOCTYPE html>\n<?xml version=\"1.0\"?>"));
    }

    [Fact]
    public void HandlesNestedPropertyTag()
    {
        // <ItemTemplate> inside a control has no prefix, so it is not reported
        var tags = DothtmlScanner.ScanTags("<dot:Repeater><ItemTemplate>x</ItemTemplate></dot:Repeater>");
        Assert.Single(tags);
    }

    [Fact]
    public void SkipsServerSideComment()
    {
        // A control commented out this way is never rendered, so validating it would report
        // an error for code the user has deliberately switched off
        var tags = DothtmlScanner.ScanTags("<%-- <dot:Button /> --%><dot:TextBox />");

        Assert.Single(tags);
        Assert.Equal("TextBox", tags[0].TagName);
    }

    [Fact]
    public void SkipsMultilineServerSideComment()
    {
        var tags = DothtmlScanner.ScanTags("<%--\n<dot:Button />\n--%>\n<dot:TextBox />");

        Assert.Single(tags);
        Assert.Equal(3, tags[0].Line);
    }

    [Fact]
    public void SkipsUnterminatedServerSideCommentToEndOfFile()
    {
        Assert.Empty(DothtmlScanner.ScanTags("<%-- <dot:Button />"));
    }

    [Fact]
    public void ReportsTagAfterTextThatOnlyLooksLikeACloser()
    {
        // Without an opener it is not a comment; treating it as one would hide real markup
        var tags = DothtmlScanner.ScanTags("a --%> <dot:Button />");

        Assert.Single(tags);
        Assert.Equal("Button", tags[0].TagName);
    }
}
