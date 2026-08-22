package com.keeper7.dotvvm.lang

import com.intellij.lang.html.HTMLParserDefinition
import com.intellij.psi.FileViewProvider
import com.intellij.psi.PsiFile
import com.intellij.psi.impl.source.html.HtmlFileImpl
import com.intellij.psi.tree.IFileElementType
import com.keeper7.dotvvm.binding.DotvvmMaskingLexer

/**
 * Registering [HTMLParserDefinition] directly for DotVVM is not enough: it builds the PSI file
 * with `HTMLLanguage` hardcoded, so `psiFile.language` would never return [DotvvmLanguage].
 * Overriding [getFileNodeType] and [createFile] keeps all HTML parsing intact while the file
 * reports itself as DotVVM.
 */
class DotvvmParserDefinition : HTMLParserDefinition() {

    override fun getFileNodeType(): IFileElementType = FILE

    override fun createFile(viewProvider: FileViewProvider): PsiFile =
        HtmlFileImpl(viewProvider, FILE)

    /**
     * A binding expression may contain quotes; the HTML lexer would end the attribute value there.
     */
    override fun createLexer(project: com.intellij.openapi.project.Project?): com.intellij.lexer.Lexer =
        DotvvmMaskingLexer(super.createLexer(project))

    companion object {
        @JvmField
        val FILE = IFileElementType("DOTVVM_FILE", DotvvmLanguage.INSTANCE)
    }
}
