package com.keeper7.dotvvm.directive

import com.intellij.openapi.application.ApplicationManager
import com.intellij.testFramework.fixtures.CompletionAutoPopupTestCase

/**
 * That the popup opens on the at sign **itself**, with nothing typed after it.
 *
 * Two tests used to stand here and both called `DirectiveCompletionContributor.invokeAutoPopup`
 * directly. That proved only that the method returns true — which is exactly the trap this
 * project has been caught by before: a test calling the plugin's own method says nothing about
 * whether the platform ever asks. The method has since gone (the platform deprecated it) and
 * the work moved to [DotvvmAutoPopup][com.keeper7.dotvvm.ide.DotvvmAutoPopup], so there is
 * nothing left to call. Typing the character is the only honest way to ask.
 *
 * `DirectiveTabInsertTest` does not cover this: it types `@vie`, and letters open a popup on
 * their own. Only a single at sign tells the two triggers apart.
 */
class DirectiveAtSignPopupTest : CompletionAutoPopupTestCase() {

    /** The fixture runs the test off the EDT, and the lookup may only be read on it. */
    private fun popupIsOpen(): Boolean {
        var open = false
        ApplicationManager.getApplication().invokeAndWait { open = myFixture.lookup != null }
        return open
    }

    fun testPopupOpensRightAfterAtSign() {
        myFixture.configureByText("A.dothtml", "<caret>\n<html></html>")

        type("@")

        assertTrue("The popup must open as soon as @ is typed", popupIsOpen())
    }

    fun testPopupDoesNotOpenInsideBody() {
        // An at sign in page text does not introduce a directive
        myFixture.configureByText("B.dothtml", "<html><body><caret></body></html>")

        type("@")

        assertFalse(popupIsOpen())
    }
}
