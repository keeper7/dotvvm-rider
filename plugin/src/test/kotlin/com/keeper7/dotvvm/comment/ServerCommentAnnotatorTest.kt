package com.keeper7.dotvvm.comment

import com.intellij.testFramework.fixtures.BasePlatformTestCase

class ServerCommentAnnotatorTest : BasePlatformTestCase() {

    private fun colouredRanges(text: String): List<String> {
        myFixture.configureByText("Sample.dothtml", text)
        return myFixture.doHighlighting()
            .filter { it.forcedTextAttributesKey?.externalName == "DOTVVM_COMMENT_IN_TAG" }
            .map { text.substring(it.startOffset, it.endOffset) }
    }

    fun testCommentBetweenAttributesIsColoured() {
        // The masker blanks this one out, so without the annotator it would be plain whitespace
        val coloured = colouredRanges("<th <%-- width=\"30%\" --%>>x</th>")
        assertContainsElements(coloured, "<%-- width=\"30%\" --%>")
    }

    fun testCommentOutsideATagIsLeftToTheLexer() {
        // Annotating it too would paint the same colour twice for nothing
        assertEmpty(colouredRanges("<div><%-- note --%></div>"))
    }
}
