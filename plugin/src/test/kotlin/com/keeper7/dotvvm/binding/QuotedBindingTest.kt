package com.keeper7.dotvvm.binding

import com.intellij.psi.PsiErrorElement
import com.intellij.psi.util.PsiTreeUtil
import com.intellij.psi.xml.XmlTag
import com.intellij.testFramework.fixtures.BasePlatformTestCase

class QuotedBindingTest : BasePlatformTestCase() {

    fun testQuoteInsideBindingDoesNotEndAttribute() {
        // DotVVM supports this form; HTML would read the quote as the end of the value
        val file = myFixture.configureByText(
            "A.dotcontrol",
            "<cc:X ValueChanged=\"{staticCommand: _c.A = _c.B ?? \"\"}\" Enabled=\"{value: _c.E}\" />"
        )

        val errors = PsiTreeUtil.findChildrenOfType(file, PsiErrorElement::class.java)
        assertEmpty("Parser reports: " + errors.joinToString { it.errorDescription }, errors)

        val tag = PsiTreeUtil.findChildOfType(file, XmlTag::class.java)!!
        assertEquals("cc:X", tag.name)
        assertNotNull("The second attribute was lost", tag.getAttribute("Enabled"))
    }

    fun testFileTextIsNotModified() {
        // Masking is for the lexer only; the document must keep the original quotes
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
