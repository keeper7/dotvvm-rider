package com.keeper7.dotvvm.binding

import com.intellij.psi.tree.IElementType

class BindingTokenType(debugName: String) : IElementType(debugName, BindingLanguage.INSTANCE)

object BindingTokenTypes {
    @JvmField val LBRACE = BindingTokenType("LBRACE")
    @JvmField val RBRACE = BindingTokenType("RBRACE")
    @JvmField val KEYWORD = BindingTokenType("KEYWORD")
    @JvmField val COLON = BindingTokenType("COLON")
    @JvmField val IDENTIFIER = BindingTokenType("IDENTIFIER")
    @JvmField val STRING = BindingTokenType("STRING")
    @JvmField val NUMBER = BindingTokenType("NUMBER")
    @JvmField val OPERATOR = BindingTokenType("OPERATOR")
    @JvmField val PAREN = BindingTokenType("PAREN")
    @JvmField val WHITE_SPACE = BindingTokenType("WHITE_SPACE")
    @JvmField val BAD_CHARACTER = BindingTokenType("BAD_CHARACTER")
}
