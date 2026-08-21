package com.keeper7.dotvvm.binding

import com.intellij.psi.PsiErrorElement
import com.intellij.psi.util.PsiTreeUtil
import com.intellij.psi.xml.XmlTag
import com.intellij.testFramework.fixtures.BasePlatformTestCase

class QuotedBindingTest : BasePlatformTestCase() {

    fun testQuoteInsideBindingDoesNotEndAttribute() {
        // DotVVM tento zápis podporuje; HTML by uvozovku brala jako konec hodnoty
        val file = myFixture.configureByText(
            "A.dotcontrol",
            "<cc:X ValueChanged=\"{staticCommand: _c.A = _c.B ?? \"\"}\" Enabled=\"{value: _c.E}\" />"
        )

        val errors = PsiTreeUtil.findChildrenOfType(file, PsiErrorElement::class.java)
        assertEmpty("Parser hlásí: " + errors.joinToString { it.errorDescription }, errors)

        val tag = PsiTreeUtil.findChildOfType(file, XmlTag::class.java)!!
        assertEquals("cc:X", tag.name)
        assertNotNull("Druhý atribut se ztratil", tag.getAttribute("Enabled"))
    }

    fun testFileTextIsNotModified() {
        // Maskování je jen pro lexer; v dokumentu musí zůstat původní uvozovky
        val text = "<cc:X A=\"{staticCommand: B ?? \"\"}\" />"
        val file = myFixture.configureByText("B.dotcontrol", text)
        assertEquals(text, file.text)
    }

    fun testPlainAttributesAreUnaffected() {
        val file = myFixture.configureByText(
            "C.dotcontrol", "<div class=\"a\" id=\"b\">x</div>")
        assertEmpty(PsiTreeUtil.findChildrenOfType(file, PsiErrorElement::class.java))

        val tag = PsiTreeUtil.findChildOfType(file, XmlTag::class.java)!!
        assertEquals("a", tag.getAttribute("class")?.value)
        assertEquals("b", tag.getAttribute("id")?.value)
    }
}
