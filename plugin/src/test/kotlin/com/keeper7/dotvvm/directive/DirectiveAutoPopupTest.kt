package com.keeper7.dotvvm.directive

import com.intellij.testFramework.fixtures.BasePlatformTestCase

class DirectiveAutoPopupTest : BasePlatformTestCase() {

    private fun autoPopupAfterAt(text: String): Boolean {
        val file = myFixture.configureByText("A.dothtml", text)
        val caret = myFixture.editor.caretModel.offset
        val element = file.findElementAt((caret - 1).coerceAtLeast(0))!!
        return DirectiveCompletionContributor().invokeAutoPopup(element, '@')
    }

    fun testPopupOpensRightAfterAtSign() {
        assertTrue("The popup must open as soon as @ is typed", autoPopupAfterAt("@<caret>\n<html></html>"))
    }

    fun testPopupDoesNotOpenInsideBody() {
        // An at sign in page text does not introduce a directive
        assertFalse(autoPopupAfterAt("<html><body>@<caret></body></html>"))
    }

    fun testOnlyDirectivesAreOfferedAfterAtSign() {
        // After @ there is no point offering HTML tags: the user is typing a directive
        // The prefix "v" also matches the HTML tags <var> and <video>, which is why it is used
        myFixture.configureByText("B.dothtml", "@v<caret>\n<html></html>")
        myFixture.completeBasic()

        val suggestions = myFixture.lookupElementStrings ?: emptyList()
        assertContainsElements(suggestions, "viewModel", "viewModule")
        assertDoesntContain(suggestions, "video", "var")
    }
}
