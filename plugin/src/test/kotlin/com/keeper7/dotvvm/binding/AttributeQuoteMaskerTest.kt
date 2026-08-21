package com.keeper7.dotvvm.binding

import org.junit.Assert.assertEquals
import org.junit.Assert.assertSame
import org.junit.Test

class AttributeQuoteMaskerTest {

    @Test fun masksQuotesInsideBinding() {
        val text = "<x A=\"{staticCommand: B ?? \"\"}\" C=\"{value: D}\" />"
        val masked = AttributeQuoteMasker.mask(text).toString()

        assertEquals("délka se nesmí změnit", text.length, masked.length)
        assertEquals("<x A=\"{staticCommand: B ??   }\" C=\"{value: D}\" />", masked)
    }

    @Test fun leavesPlainAttributesAlone() {
        val text = "<div class=\"a\" id=\"b\">x</div>"
        assertSame("bez bindingu se nemá kopírovat", text, AttributeQuoteMasker.mask(text))
    }

    @Test fun leavesBindingWithoutQuotesAlone() {
        val text = "<x A=\"{value: B}\" />"
        assertSame(text, AttributeQuoteMasker.mask(text))
    }

    @Test fun keepsClosingQuoteOfTheValue() {
        // Uvozovka ukončující hodnotu je mimo binding a musí zůstat
        val text = "<x A=\"{value: B}\" C=\"d\" />"
        assertSame(text, AttributeQuoteMasker.mask(text))
    }

    @Test fun handlesNestedBraces() {
        val text = "<x A=\"{value: new { P = \"\" }}\" />"
        val masked = AttributeQuoteMasker.mask(text).toString()
        assertEquals(text.length, masked.length)
        assertEquals("<x A=\"{value: new { P =    }}\" />", masked)
    }
}
