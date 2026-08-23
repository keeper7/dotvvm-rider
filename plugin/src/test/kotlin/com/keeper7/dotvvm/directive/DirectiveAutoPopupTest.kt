package com.keeper7.dotvvm.directive

import com.intellij.codeInsight.completion.CompletionContributor
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
        assertContainsElements(suggestions, "viewModel")
        assertDoesntContain(suggestions, "video", "var")
        // `viewModule` is not a DotVVM directive — the view module one is called `js`
        assertDoesntContain(suggestions, "viewModule")
    }

    fun testThePlatformFindsTheContributorAtAll() {
        // The one that matters: the platform looks for an auto-popup contributor by the
        // *element's* language, and the tokens in a .dothtml file carry XmlLanguage even though
        // the file is DotVVM. Registered for DotVVM, the contributor was never asked and typing
        // @ opened nothing — while the test above, which calls the method directly, stayed green.
        val file = myFixture.configureByText("A.dothtml", "@<caret>\n<html></html>")
        val element = file.findElementAt(myFixture.editor.caretModel.offset - 1)!!

        val contributors = CompletionContributor.forLanguage(element.language)

        assertTrue(
            "No DirectiveCompletionContributor for ${element.language.id}, " +
            "which is the language of ${element.javaClass.simpleName}",
            contributors.any { it is DirectiveCompletionContributor })
    }

    fun testDirectiveNamesAreNotOfferedInAValue() {
        // 141 items used to come up here: 11 directive names and 130 HTML tags, with the
        // server's view models behind all of them
        myFixture.configureByText("C.dothtml", "@viewModel <caret>\n<html></html>")
        myFixture.completeBasic()

        val suggestions = myFixture.lookupElementStrings ?: emptyList()
        assertEmpty("Nothing local belongs in a directive's value: $suggestions", suggestions)
    }

    fun testTagsAreStillOfferedInTheBody() {
        // The filter must not reach past the header
        myFixture.configureByText("D.dothtml", "<html><div <caret>></div></html>")
        myFixture.completeBasic()

        assertNotEmpty(myFixture.lookupElementStrings ?: emptyList<String>())
    }
}
