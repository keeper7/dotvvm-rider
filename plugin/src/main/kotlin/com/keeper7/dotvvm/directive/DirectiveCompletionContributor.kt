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

        // Nothing in the file header is an HTML tag, wherever in it the caret stands. The tags
        // cannot be shut out with stopHere(), which would take the LSP server's answer with
        // them — measured: 130 tags ahead of the view models the user came for.
        result.runRemainingContributors(parameters) { completionResult ->
            if (!completionResult.lookupElement.lookupString.startsWith('<')) {
                result.passResult(completionResult)
            }
        }
    }

    /**
     * Opens the popup as soon as `@` is typed, without waiting for another character;
     * otherwise completion would only start from the second letter.
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

    /** Whether the word being typed starts with an at sign. */
    private fun startsWithAtSign(text: String, offset: Int): Boolean {
        var i = offset.coerceIn(0, text.length)
        while (i > 0 && text[i - 1].isLetter()) i--
        return i > 0 && text[i - 1] == '@'
    }

    /**
     * Directives live only in the file header. The position is judged by whether the document
     * body has already started: after the first tag or DOCTYPE there can be no directive.
     */
    private fun isInDirectiveArea(text: String, offset: Int): Boolean {
        val before = text.take(offset)
        return before.lineSequence().none { it.trimStart().startsWith('<') }
    }
}
