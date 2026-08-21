package com.keeper7.dotvvm.directive

import com.intellij.psi.PsiErrorElement
import com.intellij.psi.util.PsiTreeUtil
import com.intellij.testFramework.fixtures.BasePlatformTestCase

class DirectiveParsingTest : BasePlatformTestCase() {

    fun testDoctypeAfterDirectiveIsNotAnError() {
        val file = myFixture.configureByText(
            "Sample.dothtml",
            """
            @viewModel App.MyViewModel, App

            <!DOCTYPE html>
            <html><body></body></html>
            """.trimIndent()
        )

        val errors = PsiTreeUtil.findChildrenOfType(file, PsiErrorElement::class.java)
        assertEmpty("Parser hlásí chybu: " + errors.joinToString { it.errorDescription }, errors)
    }

    fun testPlainHtmlStillParsesWithoutErrors() {
        val file = myFixture.configureByText(
            "Plain.dothtml",
            "<!DOCTYPE html>\n<html><body><div>x</div></body></html>"
        )
        assertEmpty(PsiTreeUtil.findChildrenOfType(file, PsiErrorElement::class.java))
    }

    fun testBindingsStillWorkAfterDirective() {
        // Regrese: zásah do lexeru nesmí rozbít injektáž bindingů
        val file = myFixture.configureByText(
            "Binding.dothtml",
            "@viewModel App.Vm\n<html><body><dot:TextBox Text=\"{value: Name}\" /></body></html>"
        )
        assertTrue(file.text.contains("{value: Name}"))
        assertEmpty(PsiTreeUtil.findChildrenOfType(file, PsiErrorElement::class.java))
    }
}
