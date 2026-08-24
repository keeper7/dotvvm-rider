package com.keeper7.dotvvm.binding

import com.intellij.codeInsight.lookup.LookupElementBuilder
import com.intellij.codeInsight.lookup.LookupElementDecorator
import com.intellij.codeInsight.template.impl.LiveTemplateLookupElementImpl
import com.intellij.codeInsight.template.impl.TemplateImpl
import com.intellij.testFramework.fixtures.BasePlatformTestCase
import com.keeper7.dotvvm.ide.MarkupCompletion

/**
 * Inside a binding the platform still offers markup, and it does so in two shapes: tags, whose
 * lookup string begins with the angle bracket, and **Emmet abbreviations**, which are live
 * templates named `fieldset:d`, `form`, `fig`. The second kind slipped through the filter that
 * caught the first — found in the sandbox by typing one letter inside `{{`.
 *
 * The first test is a control: without it the second would only prove that the platform offers
 * nothing here, which is how the worst bug of plan 4 slipped past.
 *
 * **Emmet itself is not available in the test platform** — measured, a plain `<div>f` offers
 * `<form` and the tags but no abbreviation - so what the predicate makes of a live template is
 * tested on one built by hand.
 */
class EmmetInBindingTest : BasePlatformTestCase() {

    private fun offered(): List<String> {
        myFixture.completeBasic()
        return myFixture.lookupElementStrings ?: emptyList()
    }

    fun testTheTagsAreOfferedInPlainMarkup() {
        myFixture.configureByText("A.dothtml", "<div>f<caret></div>")

        val items = offered()

        assertTrue("nothing offered here, so the test below would prove nothing: $items",
                   items.any { it.startsWith("<") })
    }

    fun testTheyAreNotOfferedInsideABinding() {
        myFixture.configureByText("B.dothtml", "<div>{{f<caret>}}</div>")

        val items = offered()

        assertTrue(items.toString(), items.none { it.startsWith("<") })
    }

    fun testALiveTemplateCountsAsMarkup() {
        val template = LiveTemplateLookupElementImpl(TemplateImpl("form", "<form></form>", "html"), false)

        assertTrue(MarkupCompletion.offers(template))
    }

    fun testEvenWrappedInADecorator() {
        val template = LiveTemplateLookupElementImpl(TemplateImpl("fig", "<figure></figure>", "html"), false)

        assertTrue(MarkupCompletion.offers(LookupElementDecorator.withInsertHandler(template) { _, _ -> }))
    }

    fun testAnOrdinaryItemDoesNot() {
        assertFalse(MarkupCompletion.offers(LookupElementBuilder.create("value:")))
    }
}
