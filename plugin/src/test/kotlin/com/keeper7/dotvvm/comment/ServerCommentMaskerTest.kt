package com.keeper7.dotvvm.comment

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertSame
import org.junit.Assert.assertTrue
import org.junit.Test

class ServerCommentMaskerTest {

    @Test fun masksOpenerAndCloser() {
        val masked = ServerCommentMasker.mask("<%-- hello --%>").toString()
        assertEquals("<!-- hello  -->", masked)
    }

    @Test fun keepsLengthCharacterForCharacter() {
        val text = "<div><%-- a --%></div>"
        assertEquals(text.length, ServerCommentMasker.mask(text).length)
    }

    @Test fun masksSeveralComments() {
        val masked = ServerCommentMasker.mask("<%-- a --%>x<%-- b --%>").toString()
        assertEquals("<!-- a  -->x<!-- b  -->", masked)
    }

    @Test fun handlesMultilineComment() {
        val text = "<%--\n  <dot:Button />\n--%>"
        val masked = ServerCommentMasker.mask(text).toString()
        assertEquals("<!--\n  <dot:Button />\n -->", masked)
        assertEquals(text.length, masked.length)
    }

    @Test fun masksUnterminatedOpenerToEnd() {
        // The same as HTML does with an unterminated comment: the rest of the file is comment
        val masked = ServerCommentMasker.mask("<%-- unterminated").toString()
        assertEquals("<!-- unterminated", masked)
    }

    @Test fun leavesCloserWithoutOpenerAlone() {
        // With no opener it is not a comment, and masking it would change the meaning
        assertEquals("a --%> b", ServerCommentMasker.mask("a --%> b").toString())
    }

    @Test fun leavesHtmlCommentAlone() {
        assertEquals("<!-- plain -->", ServerCommentMasker.mask("<!-- plain -->").toString())
    }

    @Test fun returnsSameInstanceWhenNothingToMask() {
        // The vast majority of files hold no server-side comment; copying them is a waste
        val text = "<div>no comments here</div>"
        assertSame(text, ServerCommentMasker.mask(text))
    }

    @Test fun doesNotTouchTextThatOnlyLooksLikeAnOpener() {
        assertEquals("100% -- done", ServerCommentMasker.mask("100% -- done").toString())
    }

    @Test fun blanksOutACommentBetweenAttributes() {
        // HTML has no comment inside a tag, so `<!--` there would read as three attributes and
        // the tag would never close. DotVVM does allow it — verified against its own tokenizer.
        val masked = ServerCommentMasker.mask("<th <%-- width=\"30%\" --%>>x</th>").toString()
        assertEquals("<th                      >x</th>", masked)
    }

    @Test fun keepsTheAttributesAroundABlankedComment() {
        val text = "<th class=\"a\" <%-- w=\"1\" --%> id=\"b\">x</th>"
        val masked = ServerCommentMasker.mask(text).toString()
        assertEquals(text.length, masked.length)
        assertTrue(masked, masked.contains("class=\"a\"") && masked.contains("id=\"b\""))
        assertFalse(masked, masked.contains("w=\"1\""))
    }

    @Test fun aClosingBracketInsideACommentDoesNotEndTheTag() {
        // Without blanking, the tag would end at the '>' inside the comment
        val masked = ServerCommentMasker.mask("<th <%-- a > b --%> id=\"x\">y</th>").toString()
        assertTrue(masked, masked.contains("id=\"x\""))
        assertFalse(masked, masked.contains("a > b"))
    }

    @Test fun aCommentInAnAttributeValueIsLeftAlone() {
        // Inside a value it is text, not a comment
        val text = "<div title=\"<%-- x --%>\">y</div>"
        assertEquals(text, ServerCommentMasker.mask(text).toString())
    }

    @Test fun stillMasksNormallyAfterATagCloses() {
        val masked = ServerCommentMasker.mask("<div id=\"a\"><%-- x --%></div>").toString()
        assertEquals("<div id=\"a\"><!-- x  --></div>", masked)
    }

    @Test fun blanksOutAnUnterminatedCommentInsideATag() {
        val masked = ServerCommentMasker.mask("<th <%-- oops").toString()
        assertEquals("<th          ", masked)
    }

    @Test fun keepsLineBreaksInsideABlankedComment() {
        // Blanking a line break would shift every line number after it, and both the LSP
        // diagnostics and the editor address text by line and column. Real comments between
        // attributes run over several lines — 9 of them in the project this was found on.
        val text = "<th <%-- a\n   b --%> id=\"x\">y</th>"
        val masked = ServerCommentMasker.mask(text).toString()

        assertEquals(text.length, masked.length)
        assertEquals(text.count { it == '\n' }, masked.count { it == '\n' })
        assertTrue(masked, masked.startsWith("<th "))
        assertTrue(masked, masked.endsWith(" id=\"x\">y</th>"))
        assertFalse(masked, masked.contains("a") && masked.contains("b"))
    }
}
