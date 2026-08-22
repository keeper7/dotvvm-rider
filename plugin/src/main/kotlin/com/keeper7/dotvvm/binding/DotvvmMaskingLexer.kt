package com.keeper7.dotvvm.binding

import com.intellij.lexer.DelegateLexer
import com.intellij.lexer.Lexer
import com.keeper7.dotvvm.comment.DotvvmMasks

/**
 * Hands the HTML lexer text in which the constructs it cannot read are masked — quotes inside
 * binding expressions, and DotVVM's server-side comments.
 *
 * Attempts to steer the lexer from outside failed: `BaseHtmlLexer` forbids restarting at a
 * different position, and stepping the delegate forward leaves it in a state outside the tag.
 * Masking sidesteps the problem before it arises — the lexer receives text that is valid HTML
 * and does its work unchanged. Offsets stay equal because every replacement is character for
 * character.
 *
 * Both the parser and the editor's highlighter run through this one class, which is what keeps
 * the tree and the colours built from the same text.
 */
class DotvvmMaskingLexer(delegate: Lexer) : DelegateLexer(delegate) {

    override fun start(buffer: CharSequence, startOffset: Int, endOffset: Int, initialState: Int) {
        super.start(DotvvmMasks.applyAll(buffer), startOffset, endOffset, initialState)
    }
}
