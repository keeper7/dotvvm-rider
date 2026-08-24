package com.keeper7.dotvvm.binding

import com.intellij.testFramework.fixtures.CompletionAutoPopupTestCase

/**
 * Drives the real auto-popup, not an explicit completion: the two differ exactly where the bug
 * was. `{{re` followed by Tab produced `<re></re>` — Emmet, reached because the popup was on
 * screen with nothing selected in it. With `completeBasic` an item is always selected, so a
 * green suite would have said nothing about the popup the user sees.
 */
class BindingTabInsertTest : CompletionAutoPopupTestCase() {

    private fun text() = myFixture.editor.document.text

    fun testTabInsertsTheKind() {
        myFixture.configureByText("A.dothtml", "<span><caret></span>")
        type("{{re")

        myFixture.type('\t')

        assertEquals("<span>{{resource:}}</span>", text())
    }

    fun testEnterInsertsItToo() {
        myFixture.configureByText("B.dothtml", "<span><caret></span>")
        type("{{va")

        myFixture.type('\n')

        assertEquals("<span>{{value:}}</span>", text())
    }

    fun testTypingOnKeepsFiltering() {
        // The selection must not freeze the list on its first item
        myFixture.configureByText("C.dothtml", "<span><caret></span>")
        type("{{sta")

        myFixture.type('\t')

        assertEquals("<span>{{staticCommand:}}</span>", text())
    }
}
