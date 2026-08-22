package com.keeper7.dotvvm.comment

import com.keeper7.dotvvm.binding.AttributeQuoteMasker

/**
 * All the rewrites the HTML lexer needs to see, in one place.
 *
 * There are two of them, and the parser and the editor's highlighter must apply them
 * identically — they run lexers of their own, and a mismatch shows up only as wrong colours
 * over a correct tree. Keeping the chain here is what stops the two from drifting apart.
 *
 * Every mask preserves length character for character, so the order does not change the result.
 */
object DotvvmMasks {

    fun applyAll(text: CharSequence): CharSequence =
        AttributeQuoteMasker.mask(ServerCommentMasker.mask(text))
}
