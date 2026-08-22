package com.keeper7.dotvvm.comment

import com.intellij.lang.injection.InjectedLanguageManager
import com.intellij.psi.PsiErrorElement
import com.intellij.psi.util.PsiTreeUtil
import com.intellij.psi.xml.XmlComment
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

    fun testBindingInsideCommentIsNotInjected() {
        // A comment is one token, so there is no XmlText or XmlAttributeValue to inject into —
        // a commented-out binding must not be treated as code
        val file = myFixture.configureByText(
            "Test.dothtml", "<div><%-- {{value: Name}} --%></div>")

        val manager = InjectedLanguageManager.getInstance(project)
        val languages = mutableListOf<String>()
        PsiTreeUtil.processElements(file) { element ->
            manager.getInjectedPsiFiles(element)?.forEach { languages.add(it.first.language.id) }
            true
        }
        assertFalse("A binding inside a comment was injected: $languages",
                    languages.contains("DotVVMBinding"))
    }

    fun testCommentElementCoversTheWholeMarker() {
        // The padding space belongs before the closer. With it after, the comment ended one
        // character early and the final '>' fell out as whitespace — unpainted in the editor.
        val text = "<div><%-- note --%></div>"
        val file = myFixture.configureByText("Test.dothtml", text)

        val comment = PsiTreeUtil.findChildOfType(file, XmlComment::class.java)!!
        assertEquals("<%-- note --%>", comment.text)
    }
}
