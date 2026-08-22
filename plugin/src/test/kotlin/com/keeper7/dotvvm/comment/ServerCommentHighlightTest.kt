package com.keeper7.dotvvm.comment

import com.intellij.openapi.editor.ex.EditorEx
import com.intellij.testFramework.fixtures.BasePlatformTestCase

/**
 * Measures the editor's own highlighter, not the PSI. With the masking only in the parser the
 * tree is right while the colours are not — and no PSI dump reveals that.
 */
class ServerCommentHighlightTest : BasePlatformTestCase() {

    private fun keysAt(text: String, offset: Int): List<String> {
        myFixture.configureByText("A.dothtml", text)
        val iterator = (myFixture.editor as EditorEx).highlighter.createIterator(offset)
        return iterator.textAttributesKeys.map { it.externalName }
    }

    fun testCommentBodyIsColouredAsComment() {
        val text = "<div><%-- note --%></div>"
        assertContainsElements(keysAt(text, text.indexOf("note")), "HTML_COMMENT")
    }

    fun testTagInsideCommentIsNotColouredAsTag() {
        // What the user reported: a commented-out control still looked like markup
        val text = "<div><%-- <dot:Button /> --%></div>"
        assertContainsElements(keysAt(text, text.indexOf("dot:Button")), "HTML_COMMENT")
    }

    fun testOpenerItselfIsColouredAsComment() {
        val text = "<div><%-- note --%></div>"
        assertContainsElements(keysAt(text, text.indexOf("<%--")), "HTML_COMMENT")
    }

    fun testMarkupAfterTheCommentIsUnaffected() {
        // The closer must end the comment; otherwise the rest of the file would go grey
        val text = "<%-- note --%><div class=\"row\">x</div>"
        assertContainsElements(keysAt(text, text.indexOf("class")), "HTML_ATTRIBUTE_NAME")
    }
}
