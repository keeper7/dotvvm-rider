package com.keeper7.dotvvm.binding

import com.intellij.openapi.application.ApplicationManager
import com.intellij.testFramework.fixtures.CompletionAutoPopupTestCase

/**
 * That the popup opens on the brace, before a single letter of the kind is typed.
 *
 * The other half of what [DotvvmAutoPopup][com.keeper7.dotvvm.ide.DotvvmAutoPopup] does, and
 * the half with a trap in it: on the doubled form the caret only stands inside a binding once
 * [BindingBraceHandler] has written the closing `}}`, which happens in the same round of typing.
 * Asking too early answers about the text from before the brace.
 *
 * `BindingTabInsertTest` types `{{re`, so it cannot tell the brace apart from the letters.
 *
 * **One brace is a binding as well** — `{value: Name}` is DotVVM's own form and the doubled one
 * is the alternative, not the rule. A test written the other way round failed, which is how the
 * assumption got caught.
 */
class BindingBracePopupTest : CompletionAutoPopupTestCase() {

    /** The fixture runs the test off the EDT, and the lookup may only be read on it. */
    private fun popupIsOpen(): Boolean {
        var open = false
        ApplicationManager.getApplication().invokeAndWait { open = myFixture.lookup != null }
        return open
    }

    fun testPopupOpensOnTheBrace() {
        myFixture.configureByText("A.dothtml", "<span><caret></span>")

        type("{")

        assertTrue("The kinds must be offered as soon as the binding is opened", popupIsOpen())
    }

    fun testPopupOpensOnTheDoubledBraceToo() {
        myFixture.configureByText("B.dothtml", "<span><caret></span>")

        type("{{")

        assertTrue("The closing braces are written in the same round", popupIsOpen())
    }

    fun testNoPopupInsideAScript() {
        // In a script a brace is ordinary punctuation, which is why the scanner skips the element
        myFixture.configureByText("C.dothtml", "<script>function f() <caret></script>")

        type("{")

        assertFalse(popupIsOpen())
    }
}
