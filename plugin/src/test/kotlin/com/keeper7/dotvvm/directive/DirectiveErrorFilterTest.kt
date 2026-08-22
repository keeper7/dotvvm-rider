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
        assertEmpty("Highlighted errors: " + found.joinToString(), found)
    }

    fun testUnclosedTagIsStillHighlighted() {
        // The filter must be narrow: it must not suppress a real error in the document body
        val found = errors(
            "Broken.dothtml",
            "@viewModel App.Vm\n\n<!DOCTYPE html>\n<html><body></html>"
        )
        assertNotEmpty(found)
    }

    fun testDoctypeWithoutDirectiveIsUntouched() {
        // With no directive the filter has nothing to do and behaviour must match plain HTML
        val found = errors(
            "Plain.dothtml",
            "<!DOCTYPE html>\n<html><body></body></html>"
        )
        assertEmpty("Highlighted errors: " + found.joinToString(), found)
    }
}
