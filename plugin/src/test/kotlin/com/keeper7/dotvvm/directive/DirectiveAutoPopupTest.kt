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
        assertTrue("Po zapsání @ se má nabídka otevřít hned", autoPopupAfterAt("@<caret>\n<html></html>"))
    }

    fun testPopupDoesNotOpenInsideBody() {
        // Zavináč v textu stránky direktivu neuvozuje
        assertFalse(autoPopupAfterAt("<html><body>@<caret></body></html>"))
    }

    fun testOnlyDirectivesAreOfferedAfterAtSign() {
        // Po @ nemá smysl nabízet HTML tagy — uživatel píše direktivu
        // Prefix "v" sedne i na HTML tagy <var> a <video>, proto právě on
        myFixture.configureByText("B.dothtml", "@v<caret>\n<html></html>")
        myFixture.completeBasic()

        val suggestions = myFixture.lookupElementStrings ?: emptyList()
        assertContainsElements(suggestions, "viewModel", "viewModule")
        assertDoesntContain(suggestions, "video", "var")
    }
}
