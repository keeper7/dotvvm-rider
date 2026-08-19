package com.keeper7.dotvvm.binding

import org.junit.Assert.assertEquals
import org.junit.Test

class BindingScannerTest {

    private fun scan(s: String) = BindingScanner.scan(s)

    @Test fun findsSimpleBinding() {
        assertEquals(listOf(BindingMatch(0, 13, BindingKind.SINGLE)), scan("{value: Name}"))
    }

    @Test fun ignoresTextWithoutBinding() {
        assertEquals(emptyList<BindingMatch>(), scan("plain text without braces"))
    }

    @Test fun ignoresUnknownBindingKeyword() {
        assertEquals(emptyList<BindingMatch>(), scan("{unknown: Name}"))
    }

    @Test fun findsBindingInsideSurroundingText() {
        val r = scan("text before {value: X} and after")
        assertEquals(1, r.size)
        assertEquals(12, r[0].start)
        assertEquals(22, r[0].end)
    }

    @Test fun handlesNestedBracesInLambda() {
        val s = "{value: Items.Where(x => x.Id > 0).Select(x => new { A = x.Name })}"
        val r = scan(s)
        assertEquals(1, r.size)
        assertEquals(0, r[0].start)
        assertEquals(s.length, r[0].end)
    }

    @Test fun handlesClosingBraceInsideStringLiteral() {
        val s = "{value: Format(\"}\")}"
        val r = scan(s)
        assertEquals(1, r.size)
        assertEquals(s.length, r[0].end)
    }

    @Test fun handlesEscapedQuoteInsideString() {
        val s = "{value: Text(\"a\\\"}b\")}"
        val r = scan(s)
        assertEquals(1, r.size)
        assertEquals(s.length, r[0].end)
    }

    @Test fun recognizesDoubleBraceBinding() {
        val r = scan("{{value: Name}}")
        assertEquals(1, r.size)
        assertEquals(BindingKind.DOUBLE, r[0].kind)
        assertEquals(15, r[0].end)
    }

    @Test fun findsMultipleBindings() {
        val r = scan("{value: A} and {command: B()}")
        assertEquals(2, r.size)
        assertEquals(0, r[0].start)
        assertEquals(15, r[1].start)
    }

    @Test fun ignoresUnterminatedBinding() {
        assertEquals(emptyList<BindingMatch>(), scan("{value: Name"))
    }

    @Test fun acceptsAllKnownBindingKinds() {
        for (kw in listOf("value", "command", "staticCommand", "resource",
                          "controlProperty", "controlCommand", "_control", "_parent", "_root")) {
            val s = "{$kw: X}"
            assertEquals("selhalo pro $kw", 1, scan(s).size)
        }
    }
}
