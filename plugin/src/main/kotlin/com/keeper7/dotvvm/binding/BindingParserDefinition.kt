package com.keeper7.dotvvm.binding

import com.intellij.extapi.psi.ASTWrapperPsiElement
import com.intellij.extapi.psi.PsiFileBase
import com.intellij.lang.ASTNode
import com.intellij.lang.ParserDefinition
import com.intellij.lang.PsiParser
import com.intellij.lexer.Lexer
import com.intellij.openapi.fileTypes.LanguageFileType
import com.intellij.openapi.project.Project
import com.intellij.psi.FileViewProvider
import com.intellij.psi.PsiElement
import com.intellij.psi.PsiFile
import com.intellij.psi.tree.IElementType
import com.intellij.psi.tree.IFileElementType
import com.intellij.psi.tree.TokenSet
import com.keeper7.dotvvm.ide.DotvvmIcons

class BindingFile(viewProvider: FileViewProvider) : PsiFileBase(viewProvider, BindingLanguage.INSTANCE) {
    override fun getFileType() = BindingFileType.INSTANCE
    override fun toString() = "DotVVM Binding"
}

class BindingFileType private constructor() : LanguageFileType(BindingLanguage.INSTANCE) {
    override fun getName() = "DotVVM Binding"
    override fun getDescription() = "DotVVM binding expression"
    override fun getDefaultExtension() = "dotbinding"
    override fun getIcon() = DotvvmIcons.DotHtml

    companion object { @JvmField val INSTANCE = BindingFileType() }
}

/**
 * The parser stays flat: an expression tree is not needed for the MVP, since the LSP server
 * handles the semantics. Injection does require the language to have a ParserDefinition.
 */
class BindingParserDefinition : ParserDefinition {

    override fun createLexer(project: Project?): Lexer = BindingLexer()

    override fun createParser(project: Project?): PsiParser = PsiParser { root, builder ->
        val marker = builder.mark()
        while (!builder.eof()) builder.advanceLexer()
        marker.done(root)
        builder.treeBuilt
    }

    override fun getFileNodeType(): IFileElementType = FILE

    override fun getCommentTokens(): TokenSet = TokenSet.EMPTY

    override fun getStringLiteralElements(): TokenSet = TokenSet.create(BindingTokenTypes.STRING)

    override fun createElement(node: ASTNode): PsiElement = ASTWrapperPsiElement(node)

    override fun createFile(viewProvider: FileViewProvider): PsiFile = BindingFile(viewProvider)

    companion object {
        @JvmField val FILE = IFileElementType(BindingLanguage.INSTANCE)
    }
}
