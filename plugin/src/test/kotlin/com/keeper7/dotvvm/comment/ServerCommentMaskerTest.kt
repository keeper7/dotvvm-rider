package com.keeper7.dotvvm.comment

import org.junit.Assert.assertEquals
import org.junit.Assert.assertSame
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
}
