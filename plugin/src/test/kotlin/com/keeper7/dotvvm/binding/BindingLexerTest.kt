package com.keeper7.dotvvm.binding

import com.intellij.psi.tree.IElementType
import com.intellij.testFramework.fixtures.BasePlatformTestCase

/**
 * Extends BasePlatformTestCase because IElementType registers itself in a global platform
 * registry; in a plain JUnit test without an Application its construction
 * BindingTokenType selhala.
 *
 * BindingScannerTest, by contrast, is a plain JUnit test because the scanner uses no IntelliJ
 * API at all. That split is deliberate and described in the file structure.
 */
class BindingLexerTest : BasePlatformTestCase() {

    private fun tokens(text: String): List<Pair<IElementType, String>> {
        val lexer = BindingLexer()
        lexer.start(text, 0, text.length, 0)
        val out = mutableListOf<Pair<IElementType, String>>()
        while (lexer.tokenType != null) {
            out.add(lexer.tokenType!! to text.substring(lexer.tokenStart, lexer.tokenEnd))
            lexer.advance()
        }
        return out
    }

    fun testLexesBindingKeywordAndIdentifier() {
        val t = tokens("{value: Name}")
        assertEquals(BindingTokenTypes.LBRACE, t[0].first)
        assertEquals(BindingTokenTypes.KEYWORD, t[1].first)
        assertEquals("value", t[1].second)
        assertEquals(BindingTokenTypes.COLON, t[2].first)
        assertEquals(BindingTokenTypes.IDENTIFIER, t[4].first)
        assertEquals("Name", t[4].second)
        assertEquals(BindingTokenTypes.RBRACE, t[5].first)
    }

    fun testLexesStringLiteralWithEscape() {
        val t = tokens("\"a\\\"b\"")
        assertEquals(1, t.size)
        assertEquals(BindingTokenTypes.STRING, t[0].first)
    }

    fun testLexesNumbersAndOperators() {
        val t = tokens("1 + 2 >= 3")
        assertEquals(BindingTokenTypes.NUMBER, t[0].first)
        assertEquals(BindingTokenTypes.OPERATOR, t[2].first)
        assertEquals(BindingTokenTypes.OPERATOR, t[6].first)
        assertEquals(">=", t[6].second)
    }

    fun testLexesLambdaArrowAsSingleToken() {
        val t = tokens("x => x")
        assertEquals(BindingTokenTypes.OPERATOR, t[2].first)
        assertEquals("=>", t[2].second)
    }

    fun testLexesNullCoalescing() {
        val t = tokens("a ?? b")
        assertEquals("??", t[2].second)
    }

    fun testCoversWholeInputWithoutGaps() {
        val text = "{value: Items.Select(x => x.Name)}"
        val lexer = BindingLexer()
        lexer.start(text, 0, text.length, 0)
        var pos = 0
        while (lexer.tokenType != null) {
            assertEquals("gap in coverage at offset $pos", pos, lexer.tokenStart)
            pos = lexer.tokenEnd
            lexer.advance()
        }
        assertEquals(text.length, pos)
    }
}
