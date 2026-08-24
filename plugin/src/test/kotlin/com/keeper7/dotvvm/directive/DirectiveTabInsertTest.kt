package com.keeper7.dotvvm.directive

import com.intellij.openapi.application.ApplicationManager
import com.intellij.testFramework.fixtures.CompletionAutoPopupTestCase

/**
 * Drives the real auto-popup, not an explicit completion: the two differ exactly where the bug
 * was. With `completeBasic` an item is selected and Tab has always worked, which is why a green
 * suite said nothing about the popup the user actually sees.
 */
class DirectiveTabInsertTest : CompletionAutoPopupTestCase() {

    /** Editor models may only be read from the EDT, and this fixture runs the test off it. */
    private fun onEdt(block: () -> Boolean): Boolean {
        var result = false
        ApplicationManager.getApplication().invokeAndWait { result = block() }
        return result
    }

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

    fun testTheValueHalfIsSelectedToo() {
        // Without a selection Tab reached Emmet and turned `@masterPage Vi` into
        // `@masterPage <Vi></Vi>` — markup in the middle of a header. The values themselves
        // come from the server, which does not run here, so this checks the condition that
        // guards them.
        myFixture.configureByText("E.dothtml", "@viewModel A\n<caret>\n<html></html>")
        type("@masterPage Vi")

        assertTrue("the caret must count as being in the header", onEdt {
            com.keeper7.dotvvm.ide.DotvvmLookupFocus().isInHeader(myFixture.file, myFixture.editor.caretModel.offset)
        })
    }

    fun testTheBodyIsNotTheHeader() {
        myFixture.configureByText("F.dothtml", "@viewModel A\n<html><div <caret>></div></html>")

        assertFalse(onEdt {
            com.keeper7.dotvvm.ide.DotvvmLookupFocus().isInHeader(myFixture.file, myFixture.editor.caretModel.offset)
        })
    }
}
