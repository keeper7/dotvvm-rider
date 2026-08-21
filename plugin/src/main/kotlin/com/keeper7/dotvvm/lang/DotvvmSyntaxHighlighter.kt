package com.keeper7.dotvvm.lang

import com.intellij.ide.highlighter.HtmlFileHighlighter
import com.intellij.lexer.Lexer
import com.intellij.openapi.fileTypes.SyntaxHighlighter
import com.intellij.openapi.fileTypes.SyntaxHighlighterFactory
import com.intellij.openapi.project.Project
import com.intellij.openapi.vfs.VirtualFile
import com.keeper7.dotvvm.binding.QuotedBindingLexer

/**
 * HTML zvýrazňovač, který maskuje uvozovky uvnitř bindingů stejně jako parser.
 *
 * Editor nebarví podle PSI, ale vlastním lexerem ze `SyntaxHighlighter`. Kdyby maskování
 * bylo jen v `DotvvmParserDefinition`, strom by byl správný, ale barvy ne — hodnota
 * atributu by v editoru končila u první uvozovky uvnitř výrazu a zbytek tagu by zšedl.
 */
class DotvvmSyntaxHighlighter : HtmlFileHighlighter() {

    override fun getHighlightingLexer(): Lexer = QuotedBindingLexer(super.getHighlightingLexer())
}

class DotvvmSyntaxHighlighterFactory : SyntaxHighlighterFactory() {

    override fun getSyntaxHighlighter(project: Project?, virtualFile: VirtualFile?): SyntaxHighlighter =
        DotvvmSyntaxHighlighter()
}
