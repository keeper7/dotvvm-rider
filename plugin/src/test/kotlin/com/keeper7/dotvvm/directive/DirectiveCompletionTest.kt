package com.keeper7.dotvvm.directive

import com.intellij.testFramework.fixtures.BasePlatformTestCase

class DirectiveCompletionTest : BasePlatformTestCase() {

    fun testCompletesDirectiveNamesAtFileStart() {
        myFixture.configureByText("Sample.dothtml", "@<caret>\n<html></html>")
        myFixture.completeBasic()

        val suggestions = myFixture.lookupElementStrings ?: emptyList()
        assertContainsElements(suggestions, "viewModel", "masterPage", "import")
    }

    fun testDoesNotCompleteInsideBody() {
        // There are no directives inside the document body; offering them would mislead
        myFixture.configureByText("Sample.dothtml", "<html><body>@<caret></body></html>")
        myFixture.completeBasic()

        val suggestions = myFixture.lookupElementStrings ?: emptyList()
        assertDoesntContain(suggestions, "viewModel")
    }
}
