package com.keeper7.dotvvm.binding

import com.intellij.lexer.DelegateLexer
import com.intellij.lexer.Lexer

/**
 * Hands the HTML lexer text in which quotes inside binding expressions are masked.
 *
 * Attempts to steer the lexer from outside failed: `BaseHtmlLexer` forbids restarting at a
 * different position, and stepping the delegate forward leaves it in a state outside the tag.
 * Masking sidesteps the problem before it arises — the lexer receives text that is valid HTML
 * and does its work unchanged. Offsets stay equal because the replacement is character for
 * character.
 */
class QuotedBindingLexer(delegate: Lexer) : DelegateLexer(delegate) {

    override fun start(buffer: CharSequence, startOffset: Int, endOffset: Int, initialState: Int) {
        super.start(AttributeQuoteMasker.mask(buffer), startOffset, endOffset, initialState)
    }
}
