package com.keeper7.dotvvm.lang

import com.intellij.ide.highlighter.HtmlFileHighlighter
import com.intellij.lexer.Lexer
import com.intellij.openapi.fileTypes.SyntaxHighlighter
import com.intellij.openapi.fileTypes.SyntaxHighlighterFactory
import com.intellij.openapi.project.Project
import com.intellij.openapi.vfs.VirtualFile
import com.keeper7.dotvvm.binding.DotvvmMaskingLexer

/**
 * An HTML highlighter that masks the same constructs as the parser does.
 *
 * The editor does not paint from the PSI but from a lexer of its own obtained from
 * `SyntaxHighlighter`. With the masking only in `DotvvmParserDefinition` the tree would be
 * right and the colours wrong: the attribute value would end at the first quote inside a
 * binding expression, and a server-side comment would be painted as the markup it hides.
 */
class DotvvmSyntaxHighlighter : HtmlFileHighlighter() {

    override fun getHighlightingLexer(): Lexer = DotvvmMaskingLexer(super.getHighlightingLexer())
}

class DotvvmSyntaxHighlighterFactory : SyntaxHighlighterFactory() {

    override fun getSyntaxHighlighter(project: Project?, virtualFile: VirtualFile?): SyntaxHighlighter =
        DotvvmSyntaxHighlighter()
}
