package com.keeper7.dotvvm.binding

import com.intellij.lexer.LexerBase
import com.intellij.psi.tree.IElementType

class BindingLexer : LexerBase() {
    private var buffer: CharSequence = ""
    private var endOffset = 0
    private var pos = 0
    private var tokenStartOffset = 0
    private var tokenEndOffset = 0
    private var currentToken: IElementType? = null

    private val keywords = setOf(
        "value", "command", "staticCommand", "resource",
        "controlProperty", "controlCommand", "_control", "_parent", "_root", "_this", "_index"
    )

    override fun start(buffer: CharSequence, startOffset: Int, endOffset: Int, initialState: Int) {
        this.buffer = buffer
        this.endOffset = endOffset
        this.pos = startOffset
        advance()
    }

    override fun getState(): Int = 0
    override fun getTokenType(): IElementType? = currentToken
    override fun getTokenStart(): Int = tokenStartOffset
    override fun getTokenEnd(): Int = tokenEndOffset
    override fun getBufferSequence(): CharSequence = buffer
    override fun getBufferEnd(): Int = endOffset

    override fun advance() {
        tokenStartOffset = pos
        if (pos >= endOffset) { currentToken = null; return }

        val c = buffer[pos]
        currentToken = when {
            c.isWhitespace() -> { while (pos < endOffset && buffer[pos].isWhitespace()) pos++
                                  BindingTokenTypes.WHITE_SPACE }
            c == '{' -> { pos++; BindingTokenTypes.LBRACE }
            c == '}' -> { pos++; BindingTokenTypes.RBRACE }
            c == ':' -> { pos++; BindingTokenTypes.COLON }
            c == '(' || c == ')' || c == '[' || c == ']' -> { pos++; BindingTokenTypes.PAREN }
            c == '"' || c == '\'' -> { scanString(c); BindingTokenTypes.STRING }
            c.isDigit() -> { while (pos < endOffset && (buffer[pos].isDigit() || buffer[pos] == '.')) pos++
                             BindingTokenTypes.NUMBER }
            c.isLetter() || c == '_' -> scanIdentifier()
            else -> scanOperator()
        }
        tokenEndOffset = pos
    }

    private fun scanString(quote: Char) {
        pos++
        while (pos < endOffset && buffer[pos] != quote) {
            if (buffer[pos] == '\\' && pos + 1 < endOffset) pos += 2 else pos++
        }
        if (pos < endOffset) pos++
    }

    private fun scanIdentifier(): IElementType {
        val start = pos
        while (pos < endOffset && (buffer[pos].isLetterOrDigit() || buffer[pos] == '_')) pos++
        val text = buffer.subSequence(start, pos).toString()
        // a keyword only when a colon follows
        var look = pos
        while (look < endOffset && buffer[look].isWhitespace()) look++
        val followedByColon = look < endOffset && buffer[look] == ':'
        return if (text in keywords && followedByColon) BindingTokenTypes.KEYWORD
               else BindingTokenTypes.IDENTIFIER
    }

    private fun scanOperator(): IElementType {
        val two = if (pos + 1 < endOffset) buffer.subSequence(pos, pos + 2).toString() else ""
        if (two in setOf("=>", "==", "!=", "<=", ">=", "&&", "||", "??")) {
            pos += 2
            return BindingTokenTypes.OPERATOR
        }
        val c = buffer[pos]
        pos++
        return if (c in "+-*/%<>!=.,;&|?") BindingTokenTypes.OPERATOR
               else BindingTokenTypes.BAD_CHARACTER
    }
}
