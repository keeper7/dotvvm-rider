package com.keeper7.dotvvm.binding

import com.intellij.lexer.Lexer
import com.intellij.openapi.editor.DefaultLanguageHighlighterColors as Default
import com.intellij.openapi.editor.HighlighterColors
import com.intellij.openapi.editor.colors.TextAttributesKey
import com.intellij.openapi.editor.colors.TextAttributesKey.createTextAttributesKey
import com.intellij.openapi.fileTypes.SyntaxHighlighterBase
import com.intellij.openapi.fileTypes.SyntaxHighlighterFactory
import com.intellij.openapi.project.Project
import com.intellij.openapi.vfs.VirtualFile
import com.intellij.psi.tree.IElementType

object BindingColors {
    val KEYWORD    = createTextAttributesKey("DOTVVM_BINDING_KEYWORD", Default.KEYWORD)
    val IDENTIFIER = createTextAttributesKey("DOTVVM_BINDING_IDENTIFIER", Default.INSTANCE_FIELD)
    val STRING     = createTextAttributesKey("DOTVVM_BINDING_STRING", Default.STRING)
    val NUMBER     = createTextAttributesKey("DOTVVM_BINDING_NUMBER", Default.NUMBER)
    val OPERATOR   = createTextAttributesKey("DOTVVM_BINDING_OPERATOR", Default.OPERATION_SIGN)
    val BRACE      = createTextAttributesKey("DOTVVM_BINDING_BRACE", Default.BRACES)
    val PAREN      = createTextAttributesKey("DOTVVM_BINDING_PAREN", Default.PARENTHESES)
    val BAD        = createTextAttributesKey("DOTVVM_BINDING_BAD", HighlighterColors.BAD_CHARACTER)
}

class BindingHighlighter : SyntaxHighlighterBase() {

    override fun getHighlightingLexer(): Lexer = BindingLexer()

    override fun getTokenHighlights(tokenType: IElementType): Array<TextAttributesKey> =
        when (tokenType) {
            BindingTokenTypes.KEYWORD -> pack(BindingColors.KEYWORD)
            BindingTokenTypes.IDENTIFIER -> pack(BindingColors.IDENTIFIER)
            BindingTokenTypes.STRING -> pack(BindingColors.STRING)
            BindingTokenTypes.NUMBER -> pack(BindingColors.NUMBER)
            BindingTokenTypes.OPERATOR, BindingTokenTypes.COLON -> pack(BindingColors.OPERATOR)
            BindingTokenTypes.LBRACE, BindingTokenTypes.RBRACE -> pack(BindingColors.BRACE)
            BindingTokenTypes.PAREN -> pack(BindingColors.PAREN)
            BindingTokenTypes.BAD_CHARACTER -> pack(BindingColors.BAD)
            else -> TextAttributesKey.EMPTY_ARRAY
        }
}

class BindingHighlighterFactory : SyntaxHighlighterFactory() {
    override fun getSyntaxHighlighter(project: Project?, virtualFile: VirtualFile?) =
        BindingHighlighter()
}
