package com.keeper7.dotvvm.directive

import com.intellij.testFramework.fixtures.BasePlatformTestCase

class DirectiveAnnotatorTest : BasePlatformTestCase() {

    private fun highlightedRanges(text: String, key: String): List<String> {
        myFixture.configureByText("Sample.dothtml", text)
        return myFixture.doHighlighting()
            .filter { it.forcedTextAttributesKey?.externalName == key }
            .map { text.substring(it.startOffset, it.endOffset) }
    }

    fun testDirectiveNameIsHighlighted() {
        val names = highlightedRanges(
            "@viewModel App.Vm\n<html></html>", "DOTVVM_DIRECTIVE_NAME")
        assertContainsElements(names, "@viewModel")
    }

    fun testDirectiveValueIsHighlighted() {
        val values = highlightedRanges(
            "@viewModel App.Vm\n<html></html>", "DOTVVM_DIRECTIVE_VALUE")
        assertContainsElements(values, "App.Vm")
    }

    fun testTextInBodyIsNotHighlighted() {
        // Zavináč v těle dokumentu není direktiva
        val names = highlightedRanges(
            "<html><body>@viewModel App.Vm</body></html>", "DOTVVM_DIRECTIVE_NAME")
        assertEmpty(names)
    }
}
