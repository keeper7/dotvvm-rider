package com.keeper7.dotvvm.comment

import com.intellij.psi.PsiErrorElement
import com.intellij.psi.util.PsiTreeUtil
import com.intellij.psi.xml.XmlTag
import com.intellij.testFramework.fixtures.BasePlatformTestCase

class ServerCommentParsingTest : BasePlatformTestCase() {

    fun testCommentedOutControlIsNotParsedAsTag() {
        val file = myFixture.configureByText(
            "Test.dothtml",
            "<div><%-- <dot:Button Text=\"x\" /> --%></div>")

        // No XmlTag may come out of the comment; if one did, validation would report a
        // control the user has deliberately switched off
        val tags = PsiTreeUtil.findChildrenOfType(file, XmlTag::class.java)
        assertEquals(listOf("div"), tags.map { it.name })
    }

    fun testFileTextIsNotModified() {
        // Only the lexer may see the masked text. Were it to reach the document, the plugin
        // would be rewriting the user's code.
        val text = "<div><%-- note --%></div>"
        val file = myFixture.configureByText("Test.dothtml", text)
        assertEquals(text, file.text)
    }

    fun testNoParseErrorsAroundComment() {
        val file = myFixture.configureByText(
            "Test.dothtml",
            "<div>\n    <%-- a\n       multiline note --%>\n    <span>x</span>\n</div>")
        assertEmpty(PsiTreeUtil.findChildrenOfType(file, PsiErrorElement::class.java))
    }

    fun testQuotedBindingStillWorksAlongsideComment() {
        // Both masks have to hold at once; a regression here would bring back the plan 4 bug
        val text = "<%-- note --%><dot:TextBox Changed=\"{staticCommand: A = B ?? \"\"}\" />"
        val file = myFixture.configureByText("Test.dothtml", text)
        assertEmpty(PsiTreeUtil.findChildrenOfType(file, PsiErrorElement::class.java))
        assertEquals(text, file.text)
    }
}
