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
        // Uvozovka ukončující hodnotu se musí barvit jako hodnota, ne jako text v tagu
        val offset = sample.indexOf("}\"") + 1
        assertContainsElements(keysAt(sample, offset), "HTML_ATTRIBUTE_VALUE")
    }

    fun testFollowingAttributeNameIsHighlighted() {
        // Atribut za bindingem s uvozovkami nesmí spadnout do textu
        val offset = sample.indexOf("C=")
        assertContainsElements(keysAt(sample, offset), "HTML_ATTRIBUTE_NAME")
    }

    fun testPlainAttributeIsUnaffected() {
        val text = "<div class=\"row\">x</div>"
        assertContainsElements(keysAt(text, text.indexOf("class")), "HTML_ATTRIBUTE_NAME")
    }
}
