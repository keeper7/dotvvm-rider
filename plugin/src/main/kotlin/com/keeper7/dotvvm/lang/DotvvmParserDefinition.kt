package com.keeper7.dotvvm.lang

import com.intellij.lang.html.HTMLParserDefinition
import com.intellij.psi.FileViewProvider
import com.intellij.psi.PsiFile
import com.intellij.psi.impl.source.html.HtmlFileImpl
import com.intellij.psi.tree.IFileElementType
import com.keeper7.dotvvm.binding.QuotedBindingLexer

/**
 * Registrovat pro DotVVM přímo [HTMLParserDefinition] nestačí — ta vytváří PSI soubor
 * natvrdo s `HTMLLanguage`, takže `psiFile.language` by nikdy nevrátil [DotvvmLanguage].
 * Přepsáním [getFileNodeType] a [createFile] zůstane veškeré HTML parsování zachováno,
 * ale soubor se hlásí jako DotVVM.
 */
class DotvvmParserDefinition : HTMLParserDefinition() {

    override fun getFileNodeType(): IFileElementType = FILE

    override fun createFile(viewProvider: FileViewProvider): PsiFile =
        HtmlFileImpl(viewProvider, FILE)

    /**
     * Binding výraz smí obsahovat uvozovky; HTML lexer by na nich hodnotu atributu ukončil.
     */
    override fun createLexer(project: com.intellij.openapi.project.Project?): com.intellij.lexer.Lexer =
        QuotedBindingLexer(super.createLexer(project))

    companion object {
        @JvmField
        val FILE = IFileElementType("DOTVVM_FILE", DotvvmLanguage.INSTANCE)
    }
}
