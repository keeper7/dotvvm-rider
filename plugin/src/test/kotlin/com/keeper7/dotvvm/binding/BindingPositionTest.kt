package com.keeper7.dotvvm.binding

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class BindingPositionTest {

    /** Reads the caret marked with '|', which is removed before the text is scanned. */
    private fun at(marked: String): BindingPlace? {
        val caret = marked.indexOf('|')
        return BindingPosition.at(marked.removeRange(caret, caret + 1), caret)
    }

    @Test fun theOpeningBraceIsInsideABinding() {
        assertEquals(BindingPlace(kindWritten = false), at("<span>{|</span>"))
    }

    @Test fun soIsTheKindBeingTyped() {
        assertEquals(BindingPlace(kindWritten = false), at("<span>{{va|</span>"))
    }

    @Test fun afterTheColonTheKindIsWritten() {
        assertEquals(BindingPlace(kindWritten = true), at("<span>{{value: Cus|</span>"))
    }

    @Test fun theCaretBetweenTheTwoBracesCountsAsWell() {
        assertEquals(BindingPlace(kindWritten = false), at("<span>{|{</span>"))
    }

    @Test fun aClosedBindingIsBehindTheCaret() {
        assertNull(at("<span>{{value: Name}} |</span>"))
    }

    @Test fun theCaretInsideAClosedBindingIsStillInside() {
        assertEquals(BindingPlace(kindWritten = true), at("<span>{{value: Na|me}}</span>"))
    }

    @Test fun plainTextIsNotABinding() {
        assertNull(at("<span>Hello |</span>"))
    }

    @Test fun anUnknownKeywordIsNotABinding() {
        assertNull(at("<span>{foo: bar|</span>"))
    }

    @Test fun anAttributeValueHoldsBindingsToo() {
        assertEquals(BindingPlace(kindWritten = true), at("<dot:Literal Text=\"{value: Na|\""))
    }

    @Test fun bracesInAStyleBlockAreNotBindings() {
        assertNull(at("<style>.a { value: b|"))
    }

    @Test fun bracesInAScriptAreNotBindingsEither() {
        assertNull(at("<script>var o = {value: x|"))
    }

    @Test fun aCommentedOutBindingIsNotOne() {
        assertNull(at("<%-- {{value: Name|"))
    }

    @Test fun aScriptThatEndsLeavesTheBindingsAfterItAlone() {
        assertEquals(BindingPlace(kindWritten = true),
                     at("<script>var a = {};</script><span>{{value: Na|"))
    }

    @Test fun aPageCannotBindToAControlProperty() {
        assertEquals(listOf("value:", "command:", "staticCommand:", "resource:"),
                     BindingPosition.kindsFor("Page.dothtml").map { "$it:" })
    }

    @Test fun aMarkupControlCan() {
        val kinds = BindingPosition.kindsFor("My.dotcontrol")
        assertEquals(6, kinds.size)
    }
}
