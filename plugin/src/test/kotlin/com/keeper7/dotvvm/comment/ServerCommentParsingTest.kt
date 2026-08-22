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

    fun testCommentBetweenAttributesDoesNotBreakTheTag() {
        // What the user hit: `<!--` inside a tag reads as three attributes, so the tag never
        // closed and everything after it — the closing tag included — became an error element
        val file = myFixture.configureByText(
            "Test.dothtml", "<th <%-- width=\"30%\" --%>>{{value: Name}}</th>")

        val errors = PsiTreeUtil.findChildrenOfType(file, PsiErrorElement::class.java)
        assertEmpty("Parser reports: " + errors.joinToString { it.errorDescription }, errors)

        val tag = PsiTreeUtil.findChildOfType(file, XmlTag::class.java)!!
        assertEquals("th", tag.name)
        assertEmpty(tag.attributes)
    }

    fun testAttributesAroundACommentSurvive() {
        val file = myFixture.configureByText(
            "Test.dothtml", "<th class=\"a\" <%-- w=\"1\" --%> id=\"b\">x</th>")

        val tag = PsiTreeUtil.findChildOfType(file, XmlTag::class.java)!!
        assertEquals("a", tag.getAttribute("class")?.value)
        assertEquals("b", tag.getAttribute("id")?.value)
        assertNull("The commented-out attribute must not be read", tag.getAttribute("w"))
    }

    fun testTextIsNotModifiedByBlanking() {
        val text = "<th <%-- width=\"30%\" --%>>x</th>"
        assertEquals(text, myFixture.configureByText("Test.dothtml", text).text)
    }

    fun testMultilineCommentBetweenAttributesKeepsTheLines() {
        // The shape the fixture carries: a comment inside a tag running over two lines.
        // Blanking its line break would move every line number after it, and both the LSP
        // diagnostics and the editor address text by line and column.
        val text = "<th class=\"wide\" <%-- Sortable=\"true\"\n   Direction=\"Asc\" --%> id=\"x\">y</th>"
        val file = myFixture.configureByText("Test.dothtml", text)

        assertEmpty(PsiTreeUtil.findChildrenOfType(file, PsiErrorElement::class.java))
        val tag = PsiTreeUtil.findChildOfType(file, XmlTag::class.java)!!
        assertEquals("wide", tag.getAttribute("class")?.value)
        assertEquals("x", tag.getAttribute("id")?.value)
        assertNull(tag.getAttribute("Sortable"))
        assertEquals(text, file.text)
    }
}
