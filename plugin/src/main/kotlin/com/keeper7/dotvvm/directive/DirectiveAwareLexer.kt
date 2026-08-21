package com.keeper7.dotvvm.directive

import com.intellij.lexer.DelegateLexer
import com.intellij.lexer.Lexer
import com.intellij.psi.tree.IElementType
import com.intellij.psi.xml.XmlTokenType

/**
 * Vydá direktivový blok na začátku souboru jako XML komentář a teprve zbytek souboru
 * předá HTML lexeru.
 *
 * Bez toho by direktivy propadly do HTML parseru jako text a `<!DOCTYPE>` za nimi by
 * skončil chybou `Unexpected tokens` — HTML text před DOCTYPE nepřipouští, komentář ano.
 * Zařadit blok do `getCommentTokens()` nestačí: to ovlivní jen tvorbu PSI elementu,
 * kdežto parser se rozhoduje podle konkrétních tokenů komentáře.
 */
class DirectiveAwareLexer(delegate: Lexer) : DelegateLexer(delegate) {

    private class Token(val type: IElementType, val start: Int, val end: Int)

    private var prefix: List<Token> = emptyList()
    private var index = 0

    override fun start(buffer: CharSequence, startOffset: Int, endOffset: Int, initialState: Int) {
        prefix = if (startOffset == 0) directiveTokens(buffer, endOffset) else emptyList()
        index = 0

        val delegateStart = prefix.lastOrNull()?.end ?: startOffset
        super.start(buffer, delegateStart, endOffset, initialState)
    }

    private fun directiveTokens(buffer: CharSequence, endOffset: Int): List<Token> {
        val end = DirectiveScanner.scan(buffer.toString()).lastOrNull()?.end ?: return emptyList()
        if (end <= 0 || end > endOffset) return emptyList()

        // Parser potřebuje úplný komentář: začátek, obsah, konec. Rozsahy se dělí tak,
        // aby dohromady přesně pokryly direktivový blok — jinak by se text ztratil.
        return listOf(
            Token(XmlTokenType.XML_COMMENT_START, 0, 1),
            Token(XmlTokenType.XML_COMMENT_CHARACTERS, 1, end - 1),
            Token(XmlTokenType.XML_COMMENT_END, end - 1, end)
        )
    }

    private val current: Token? get() = prefix.getOrNull(index)

    override fun getTokenType(): IElementType? = current?.type ?: super.getTokenType()

    override fun getTokenStart(): Int = current?.start ?: super.getTokenStart()

    override fun getTokenEnd(): Int = current?.end ?: super.getTokenEnd()

    override fun advance() {
        if (index < prefix.size) {
            index++
            return
        }
        super.advance()
    }

    override fun getState(): Int = if (current != null) 0 else super.getState()
}
