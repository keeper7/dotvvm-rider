package com.keeper7.dotvvm.directive

import com.intellij.codeInsight.completion.CompletionContributor
import com.intellij.codeInsight.completion.CompletionParameters
import com.intellij.codeInsight.completion.CompletionResultSet
import com.intellij.codeInsight.lookup.LookupElementBuilder
import com.intellij.psi.PsiElement
import com.keeper7.dotvvm.lang.DotControlFileType
import com.keeper7.dotvvm.lang.DotHtmlFileType
import com.keeper7.dotvvm.lang.DotMasterFileType

/**
 * Nabízí názvy direktiv. Jen názvy — hodnoty (typ ViewModelu, cesta k master page) zná
 * pouze LSP server přes registr kontrolek a plugin je hádat nebude.
 */
class DirectiveCompletionContributor : CompletionContributor() {

    override fun fillCompletionVariants(
        parameters: CompletionParameters,
        result: CompletionResultSet
    ) {
        val file = parameters.originalFile
        val fileType = file.virtualFile?.fileType
        if (fileType != DotHtmlFileType.INSTANCE &&
            fileType != DotControlFileType.INSTANCE &&
            fileType != DotMasterFileType.INSTANCE) return

        if (!isInDirectiveArea(file.text, parameters.offset)) return

        for (name in DirectiveScanner.KNOWN_NAMES) {
            result.addElement(
                LookupElementBuilder.create(name)
                    .withTypeText("DotVVM direktiva", true)
            )
        }

        // Za zavináčem uživatel píše direktivu, ne HTML tag. Bez tohoto by se do nabídky
        // připletly <var> a <video>, protože prefix "v" sedne i na ně.
        if (startsWithAtSign(file.text, parameters.offset)) result.stopHere()
    }

    /**
     * Otevře nabídku hned po zapsání `@`, bez čekání na další znak — jinak by se
     * direktiva napovídala až od druhého písmene.
     */
    override fun invokeAutoPopup(position: PsiElement, typeChar: Char): Boolean {
        if (typeChar != '@') return false

        val file = position.containingFile ?: return false
        val fileType = file.virtualFile?.fileType
        if (fileType != DotHtmlFileType.INSTANCE &&
            fileType != DotControlFileType.INSTANCE &&
            fileType != DotMasterFileType.INSTANCE) return false

        return isInDirectiveArea(file.text, position.textRange.endOffset)
    }

    /** Zda slovo, které uživatel právě píše, začíná zavináčem. */
    private fun startsWithAtSign(text: String, offset: Int): Boolean {
        var i = offset.coerceIn(0, text.length)
        while (i > 0 && text[i - 1].isLetter()) i--
        return i > 0 && text[i - 1] == '@'
    }

    /**
     * Direktivy jsou jen v hlavičce souboru. Pozice se posuzuje podle toho, zda před
     * kurzorem začalo tělo dokumentu — po prvním tagu nebo DOCTYPE už direktiva být nemůže.
     */
    private fun isInDirectiveArea(text: String, offset: Int): Boolean {
        val before = text.take(offset)
        return before.lineSequence().none { it.trimStart().startsWith('<') }
    }
}
