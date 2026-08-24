package com.keeper7.dotvvm.directive

import com.intellij.codeInsight.completion.CompletionContributor
import com.intellij.codeInsight.completion.CompletionParameters
import com.intellij.codeInsight.completion.CompletionResultSet
import com.intellij.codeInsight.lookup.LookupElementBuilder
import com.keeper7.dotvvm.ide.MarkupCompletion
import com.keeper7.dotvvm.lang.DotControlFileType
import com.keeper7.dotvvm.lang.DotHtmlFileType
import com.keeper7.dotvvm.lang.DotMasterFileType

/**
 * Offers directive names. Names only: the values (the view model type, the path to the master
 * page) are known solely to the LSP server through its control registry, and the plugin will
 * not guess them.
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

        // Names only where a name goes. In a value the server has the answer, and offering
        // `@masterPage` in the middle of a view model's type helped nobody.
        if (DirectiveScanner.isOnName(file.text, parameters.offset)) {
            for (name in DirectiveScanner.KNOWN_NAMES) {
                result.addElement(
                    LookupElementBuilder.create(name)
                        .withTypeText("DotVVM directive", true)
                )
            }

            // After an at sign the user is typing a directive, not an HTML tag. Without this,
            // <var> and <video> would join the list, because the prefix "v" matches them too.
            if (startsWithAtSign(file.text, parameters.offset)) {
                result.stopHere()
                return
            }
        }

        // Nothing in the file header is markup, wherever in it the caret stands. It cannot be
        // shut out with stopHere(), which would take the LSP server's answer with it — measured:
        // 130 tags ahead of the view models the user came for. Emmet's abbreviations belong to
        // the same list and are not tags at all; see MarkupCompletion.
        result.runRemainingContributors(parameters) { completionResult ->
            if (!MarkupCompletion.offers(completionResult.lookupElement)) {
                result.passResult(completionResult)
            }
        }
    }

    /** Whether the word being typed starts with an at sign. */
    private fun startsWithAtSign(text: String, offset: Int): Boolean {
        var i = offset.coerceIn(0, text.length)
        while (i > 0 && text[i - 1].isLetter()) i--
        return i > 0 && text[i - 1] == '@'
    }

}

/**
 * Directives live only in the file header. The position is judged by whether the document
 * body has already started: after the first tag or DOCTYPE there can be no directive.
 *
 * Shared with [DotvvmAutoPopup][com.keeper7.dotvvm.ide.DotvvmAutoPopup], which asks the same
 * question when deciding whether the at sign should open the popup.
 */
internal fun isInDirectiveArea(text: String, offset: Int): Boolean {
    val before = text.take(offset)
    return before.lineSequence().none { it.trimStart().startsWith('<') }
}
