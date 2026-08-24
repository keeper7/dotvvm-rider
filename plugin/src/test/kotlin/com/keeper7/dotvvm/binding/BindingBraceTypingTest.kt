package com.keeper7.dotvvm.binding

import com.intellij.testFramework.fixtures.BasePlatformTestCase

/**
 * The braces are written by a typed handler, so nothing short of real typing proves it: the
 * decision alone is tested by [ClosingBracesTest], and this is the other half.
 */
class BindingBraceTypingTest : BasePlatformTestCase() {

    fun testTheSecondBraceClosesTheBinding() {
        myFixture.configureByText("A.dothtml", "<span><caret></span>")

        myFixture.type("{{")

        assertEquals("<span>{{}}</span>", myFixture.editor.document.text)
    }

    fun testTheCaretStaysInsideTheBraces() {
        myFixture.configureByText("B.dothtml", "<span><caret></span>")

        myFixture.type("{{value: Name")

        assertEquals("<span>{{value: Name}}</span>", myFixture.editor.document.text)
    }

    fun testBracesThatAreThereAreLeftAlone() {
        myFixture.configureByText("C.dothtml", "<span><caret>}}</span>")

        myFixture.type("{{")

        assertEquals("<span>{{}}</span>", myFixture.editor.document.text)
    }

    fun testAnHtmlFileIsNotTouched() {
        // The handler is registered for the whole platform and has to keep to its own files
        myFixture.configureByText("D.html", "<span><caret></span>")

        myFixture.type("{{")

        assertEquals("<span>{{</span>", myFixture.editor.document.text)
    }

    fun testASingleBraceInsertsNothing() {
        myFixture.configureByText("E.dothtml", "<span><caret></span>")

        myFixture.type("{")

        assertEquals("<span>{</span>", myFixture.editor.document.text)
    }
}
