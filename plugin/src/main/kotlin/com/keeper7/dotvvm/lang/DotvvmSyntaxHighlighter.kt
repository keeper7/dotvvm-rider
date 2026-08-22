package com.keeper7.dotvvm.lang

import com.intellij.ide.highlighter.HtmlFileHighlighter
import com.intellij.lexer.Lexer
import com.intellij.openapi.fileTypes.SyntaxHighlighter
import com.intellij.openapi.fileTypes.SyntaxHighlighterFactory
import com.intellij.openapi.project.Project
import com.intellij.openapi.vfs.VirtualFile
import com.keeper7.dotvvm.binding.QuotedBindingLexer

/**
 * An HTML highlighter that masks quotes inside bindings the same way the parser does.
 *
 * The editor does not paint from the PSI but from a lexer of its own obtained from
 * `SyntaxHighlighter`. With the masking only in `DotvvmParserDefinition` the tree would be
 * right and the colours wrong: in the editor the attribute value would end at the first quote
 * inside the expression and the rest of the tag would go grey.
 */
class DotvvmSyntaxHighlighter : HtmlFileHighlighter() {

    override fun getHighlightingLexer(): Lexer = QuotedBindingLexer(super.getHighlightingLexer())
}

class DotvvmSyntaxHighlighterFactory : SyntaxHighlighterFactory() {

    override fun getSyntaxHighlighter(project: Project?, virtualFile: VirtualFile?): SyntaxHighlighter =
        DotvvmSyntaxHighlighter()
}
