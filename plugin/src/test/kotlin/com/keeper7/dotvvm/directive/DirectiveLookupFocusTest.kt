package com.keeper7.dotvvm.directive

import com.intellij.codeInsight.lookup.LookupFocusDegree
import com.intellij.codeInsight.lookup.impl.LookupImpl
import com.intellij.testFramework.fixtures.BasePlatformTestCase

class DirectiveLookupFocusTest : BasePlatformTestCase() {

    /**
     * The text has to bring up more than one item: a lone suggestion is inserted outright and
     * no lookup ever exists.
     */
    private fun focusAfterCompleting(fileName: String, text: String): LookupFocusDegree {
        myFixture.configureByText(fileName, text)
        myFixture.completeBasic()
        val lookup = myFixture.lookup as LookupImpl

        // The platform opens an auto-popup unfocused; this is the state the listener corrects
        lookup.lookupFocusDegree = LookupFocusDegree.UNFOCUSED
        DirectiveLookupFocus().activeLookupChanged(null, lookup)
        return lookup.lookupFocusDegree
    }

    fun testTheDirectivePopupIsFocused() {
        // Unfocused, the item is listed but not selected and Tab does nothing — which is what
        // the user hit, while the popup LSP opens on ':' inside a tag took Tab fine
        assertEquals(LookupFocusDegree.FOCUSED,
                     focusAfterCompleting("A.dothtml", "@<caret>\n<html></html>"))
    }

    fun testCompletionInTheBodyIsLeftAlone() {
        // Everywhere but the header the platform's own judgement stands
        assertEquals(LookupFocusDegree.UNFOCUSED,
                     focusAfterCompleting("B.dothtml", "<html><div cl<caret>></div></html>"))
    }

    fun testAnotherFileTypeIsLeftAlone() {
        assertEquals(LookupFocusDegree.UNFOCUSED,
                     focusAfterCompleting("C.html", "<html><div cl<caret>></div></html>"))
    }
}
