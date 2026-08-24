package com.keeper7.dotvvm.binding

import com.intellij.codeInsight.completion.CompletionContributor
import com.intellij.codeInsight.completion.CompletionParameters
import com.intellij.codeInsight.completion.CompletionResultSet
import com.intellij.codeInsight.lookup.LookupElementBuilder
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

}
