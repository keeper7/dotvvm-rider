package com.keeper7.dotvvm.directive

import com.intellij.codeInsight.completion.CompletionContributor
import com.intellij.codeInsight.completion.CompletionParameters
import com.intellij.codeInsight.completion.CompletionResultSet
import com.intellij.codeInsight.lookup.LookupElementBuilder
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
