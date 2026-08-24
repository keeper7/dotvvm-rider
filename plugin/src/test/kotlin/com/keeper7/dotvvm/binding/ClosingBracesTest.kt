package com.keeper7.dotvvm.binding

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class ClosingBracesTest {

    /** Reads the caret marked with '|', which is removed before the decision is made. */
    private fun needed(marked: String): Boolean {
        val caret = marked.indexOf('|')
        return ClosingBraces.needed(marked.removeRange(caret, caret + 1), caret)
    }

    @Test fun aBindingJustOpenedNeedsClosing() {
        assertTrue(needed("<span>{{|</span>"))
    }

    @Test fun soDoesOneAtTheEndOfTheFile() {
        assertTrue(needed("<span>{{|"))
    }

    @Test fun andOneInAnAttributeValue() {
        assertTrue(needed("<dot:Literal Text=\"{{|\" />"))
    }

    @Test fun oneBraceIsNotABinding() {
        assertFalse(needed("<span>{|</span>"))
    }

    @Test fun bracesThatAreAlreadyThereAreLeftAlone() {
        assertFalse(needed("<span>{{|}}</span>"))
    }

    @Test fun soIsASingleClosingBrace() {
        // Something else closed it; adding two more would leave one over
        assertFalse(needed("<span>{{|}</span>"))
    }

    @Test fun aThirdBraceIsNotABindingBeingOpened() {
        assertFalse(needed("<span>{{{|</span>"))
    }

    @Test fun aBindingThatFollowsDoesNotCountAsClosing() {
        assertTrue(needed("<span>{{| {{value: X}}</span>"))
    }

    @Test fun nothingToDoAtTheStartOfTheFile() {
        assertFalse(needed("|"))
    }
}
