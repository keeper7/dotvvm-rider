package com.keeper7.dotvvm.binding

import com.intellij.codeInsight.completion.CompletionContributor
import com.intellij.codeInsight.completion.CompletionParameters
import com.intellij.codeInsight.completion.CompletionResultSet
import com.intellij.codeInsight.lookup.LookupElementBuilder
import com.intellij.lang.injection.InjectedLanguageManager
import com.intellij.openapi.fileEditor.FileEditorManager
import com.intellij.psi.PsiElement
import com.keeper7.dotvvm.ide.MarkupCompletion

/**
 * Offers the kinds of binding, and keeps the markup out of the list.
 *
 * The kinds are the plugin's business for the same reason the directive names are: they are the
 * framework's own, a project cannot add one, and nothing has to be asked of the server to know
 * them. Getting them from the plugin also means they are there **at once** — the server's answer
 * for the same place arrives a few milliseconds later, and until it did, the list held tags.
 *
 * Inside a binding **no HTML tag is valid**, so the tags the platform contributes are dropped.
 * Without that they came first and pushed `value:` and `resource:` below the fold — sometimes,
 * which is to say whenever the server's items happened to arrive after them.
 */
class BindingCompletionContributor : CompletionContributor() {

    override fun fillCompletionVariants(
        parameters: CompletionParameters,
        result: CompletionResultSet
    ) {
        val spot = BindingLocation.at(parameters.originalFile, parameters.offset) ?: return

        // Once the colon is written the expression has begun, and what may stand there is the
        // server's answer: it alone knows the project's types
        val ours = if (spot.place.kindWritten) emptyList()
                   else BindingPosition.kindsFor(spot.fileName).map { "$it:" }

        for (kind in ours) {
            result.addElement(
                LookupElementBuilder.create(kind).withTypeText("DotVVM binding", true)
            )
        }

        // The server offers the kinds too, for the sake of any other client; here they would be
        // a second copy of what is already in the list
        val mine = ours.toSet()
        result.runRemainingContributors(parameters) { completionResult ->
            val element = completionResult.lookupElement
            if (!MarkupCompletion.offers(element) && element.lookupString !in mine) {
                result.passResult(completionResult)
            }
        }
    }

    /**
     * Opens the popup on the brace itself. The server declares `{` as a trigger character as
     * well, but the plugin's own kinds must not depend on a server that may not be there — a
     * project that has never been built has no compiler process to answer with.
     */
    override fun invokeAutoPopup(position: PsiElement, typeChar: Char): Boolean {
        if (typeChar != '{') return false

        val file = InjectedLanguageManager.getInstance(position.project)
            .getTopLevelFile(position.containingFile ?: return false) ?: return false

        // The caret, not the end of the element the typed character landed in. By the time this
        // is asked the closing braces are already written, so the element runs past the caret
        // and its end would fall outside the binding it is being asked about. The editor's
        // caret is the host's, which is why the host file is what it is read against.
        val caret = FileEditorManager.getInstance(position.project)
            .selectedTextEditor?.caretModel?.offset ?: return false

        return BindingLocation.at(file, caret) != null
    }
}
