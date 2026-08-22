package com.keeper7.dotvvm.binding

import com.intellij.openapi.editor.ex.EditorEx
import com.intellij.testFramework.fixtures.BasePlatformTestCase

class QuotedBindingHighlightTest : BasePlatformTestCase() {

    private val sample = "<cc:X A=\"{staticCommand: B ?? \"\"}\" C=\"{value: D}\" />"

    private fun keysAt(text: String, offset: Int): List<String> {
        myFixture.configureByText("A.dotcontrol", text)
        val iterator = (myFixture.editor as EditorEx).highlighter.createIterator(offset)
        return iterator.textAttributesKeys.map { it.externalName }
    }

    fun testClosingQuoteIsPartOfTheAttributeValue() {
        // The quote ending the value must be coloured as a value, not as text inside the tag
        val offset = sample.indexOf("}\"") + 1
        assertContainsElements(keysAt(sample, offset), "HTML_ATTRIBUTE_VALUE")
    }

    fun testFollowingAttributeNameIsHighlighted() {
        // The attribute after a binding with quotes must not fall into text
        val offset = sample.indexOf("C=")
        assertContainsElements(keysAt(sample, offset), "HTML_ATTRIBUTE_NAME")
    }

    fun testPlainAttributeIsUnaffected() {
        val text = "<div class=\"row\">x</div>"
        assertContainsElements(keysAt(text, text.indexOf("class")), "HTML_ATTRIBUTE_NAME")
    }
}
