package com.keeper7.dotvvm.binding

import com.intellij.testFramework.fixtures.BasePlatformTestCase

class BindingKindCompletionTest : BasePlatformTestCase() {

    private fun offered(): List<String> {
        myFixture.completeBasic()
        return myFixture.lookupElementStrings ?: emptyList()
    }

    fun testTheKindsAreOfferedInsideABinding() {
        myFixture.configureByText("A.dothtml", "<span>{{<caret>}}</span>")

        val items = offered()

        assertTrue(items.toString(), items.containsAll(
            listOf("value:", "command:", "staticCommand:", "resource:")))
    }

    fun testAPageIsNotOfferedAControlBinding() {
        myFixture.configureByText("B.dothtml", "<span>{{<caret>}}</span>")

        assertFalse(offered().contains("controlProperty:"))
    }

    fun testAMarkupControlIs() {
        myFixture.configureByText("C.dotcontrol", "<span>{{<caret>}}</span>")

        assertTrue(offered().contains("controlProperty:"))
    }

    fun testTheKindsStopOnceTheColonIsWritten() {
        // What may stand in the expression is the server's answer, and it does not run here
        myFixture.configureByText("D.dothtml", "<span>{{value: <caret>}}</span>")

        assertFalse(offered().contains("value:"))
    }

    fun testNoTagIsOfferedInsideABinding() {
        // In a binding an HTML tag is never valid. Note the test platform has no HTML schema,
        // so this proves the filter runs rather than that the platform would have offered one.
        myFixture.configureByText("E.dothtml", "<span>{{<caret>}}</span>")

        assertTrue(offered().none { it.startsWith("<") })
    }

    fun testOutsideABindingNothingIsAdded() {
        myFixture.configureByText("F.dothtml", "<span>plain <caret></span>")

        assertFalse(offered().contains("value:"))
    }
}
