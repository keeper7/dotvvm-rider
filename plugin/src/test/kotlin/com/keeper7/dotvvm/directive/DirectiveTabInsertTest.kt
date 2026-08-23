package com.keeper7.dotvvm.directive

import com.intellij.testFramework.fixtures.CompletionAutoPopupTestCase

/**
 * Drives the real auto-popup, not an explicit completion: the two differ exactly where the bug
 * was. With `completeBasic` an item is selected and Tab has always worked, which is why a green
 * suite said nothing about the popup the user actually sees.
 */
class DirectiveTabInsertTest : CompletionAutoPopupTestCase() {

    private fun firstLine() = myFixture.editor.document.text.lineSequence().first()

    fun testTabInsertsTheDirective() {
        myFixture.configureByText("A.dothtml", "<caret>\n<html></html>")
        type("@vie")

        myFixture.type('\t')

        assertEquals("@viewModel", firstLine())
    }

    fun testEnterInsertsItToo() {
        myFixture.configureByText("B.dothtml", "<caret>\n<html></html>")
        type("@vie")

        myFixture.type('\n')

        assertEquals("@viewModel", firstLine())
    }

    fun testTypingOnKeepsFiltering() {
        // The selection must not freeze the list on its first item
        myFixture.configureByText("C.dothtml", "<caret>\n<html></html>")
        type("@mas")

        myFixture.type('\t')

        assertEquals("@masterPage", firstLine())
    }

    fun testTabStillWorksInTheBody() {
        // In the body the platform selects an item on its own — which is why Tab always worked
        // inside a tag and only the directive popup was dead. Nothing here may change that.
        myFixture.configureByText("D.dothtml", "<html><div <caret>></div></html>")
        type("clas")

        myFixture.type('\t')

        assertTrue(firstLine(), firstLine().contains("class"))
    }
}
