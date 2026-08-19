package com.keeper7.dotvvm.binding

import com.intellij.testFramework.fixtures.BasePlatformTestCase

/**
 * Dědí BasePlatformTestCase, protože TextAttributesKey.createTextAttributesKey
 * zapisuje do globálního registru platformy a v čistém JUnit testu bez
 * inicializované Application selže.
 */
class BindingHighlighterTest : BasePlatformTestCase() {

    private val highlighter = BindingHighlighter()

    fun testKeywordHasAttribute() {
        assertEquals(1, highlighter.getTokenHighlights(BindingTokenTypes.KEYWORD).size)
    }

    fun testStringHasAttribute() {
        assertTrue(highlighter.getTokenHighlights(BindingTokenTypes.STRING).isNotEmpty())
    }

    fun testBadCharacterHasAttribute() {
        assertTrue(highlighter.getTokenHighlights(BindingTokenTypes.BAD_CHARACTER).isNotEmpty())
    }

    fun testWhitespaceHasNoAttribute() {
        assertEquals(0, highlighter.getTokenHighlights(BindingTokenTypes.WHITE_SPACE).size)
    }
}
