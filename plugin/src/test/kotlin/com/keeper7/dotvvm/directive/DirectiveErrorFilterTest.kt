package com.keeper7.dotvvm.directive

import com.intellij.lang.annotation.HighlightSeverity
import com.intellij.testFramework.fixtures.BasePlatformTestCase

class DirectiveErrorFilterTest : BasePlatformTestCase() {

    private fun errors(fileName: String, text: String): List<String> {
        myFixture.configureByText(fileName, text)
        return myFixture.doHighlighting()
            .filter { it.severity == HighlightSeverity.ERROR }
            .map { it.description ?: it.text ?: "?" }
    }

    fun testDoctypeAfterDirectiveIsNotHighlighted() {
        val found = errors(
            "Sample.dothtml",
            "@viewModel App.MyViewModel, App\n\n<!DOCTYPE html>\n<html><body></body></html>"
        )
        assertEmpty("Zvýrazněné chyby: " + found.joinToString(), found)
    }

    fun testUnclosedTagIsStillHighlighted() {
        // Filtr musí být úzký — skutečnou chybu v těle dokumentu potlačit nesmí
        val found = errors(
            "Broken.dothtml",
            "@viewModel App.Vm\n\n<!DOCTYPE html>\n<html><body></html>"
        )
        assertNotEmpty(found)
    }

    fun testDoctypeWithoutDirectiveIsUntouched() {
        // Bez direktivy nemá filtr co řešit a chování se nesmí lišit od holého HTML
        val found = errors(
            "Plain.dothtml",
            "<!DOCTYPE html>\n<html><body></body></html>"
        )
        assertEmpty("Zvýrazněné chyby: " + found.joinToString(), found)
    }
}
