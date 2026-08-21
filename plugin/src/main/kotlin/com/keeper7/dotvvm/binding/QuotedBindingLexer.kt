package com.keeper7.dotvvm.binding

import com.intellij.lexer.DelegateLexer
import com.intellij.lexer.Lexer

/**
 * Předá HTML lexeru text, ve kterém jsou uvozovky uvnitř binding výrazů maskované.
 *
 * Pokusy řídit lexer zvenčí selhaly: restart na jinou pozici `BaseHtmlLexer` zakazuje
 * a posouvání delegáta ho nechá ve stavu mimo tag. Maskování problém obchází dřív, než
 * vznikne — lexer dostane text, který je z pohledu HTML v pořádku, a odvede svou práci
 * beze změny. Offsety zůstávají shodné, protože náhrada je znak za znak.
 */
class QuotedBindingLexer(delegate: Lexer) : DelegateLexer(delegate) {

    override fun start(buffer: CharSequence, startOffset: Int, endOffset: Int, initialState: Int) {
        super.start(AttributeQuoteMasker.mask(buffer), startOffset, endOffset, initialState)
    }
}
