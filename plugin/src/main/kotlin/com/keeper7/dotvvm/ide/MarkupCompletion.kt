package com.keeper7.dotvvm.ide

import com.intellij.codeInsight.lookup.LookupElement
import com.intellij.codeInsight.lookup.LookupElementDecorator
import com.intellij.codeInsight.template.impl.LiveTemplateLookupElement

/**
 * Whether a suggestion the platform contributed writes markup.
 *
 * Two places in a DotVVM file hold no markup at all — the file's header and the inside of a
 * binding — and in both the platform goes on offering it, because to the platform the file is
 * HTML throughout. Dropping those suggestions is what keeps the list to what may be written.
 *
 * A tag is recognised by its lookup string, which begins with the angle bracket. **Emmet is
 * not**: its abbreviations come as live templates named `fieldset:d`, `form`, `fig`, so typing
 * `f` inside `{{` filled the list with them - measured in the sandbox. They are recognised by
 * the element instead, [CustomLiveTemplateLookupElement][com.intellij.codeInsight.template.impl.CustomLiveTemplateLookupElement]
 * being the Emmet one and a subclass of the ordinary live template's.
 */
object MarkupCompletion {

    fun offers(element: LookupElement): Boolean =
        element.lookupString.startsWith('<') || isLiveTemplate(element)

    /** Decorators are unwrapped: a contributor may hand the element on wrapped in one. */
    private fun isLiveTemplate(element: LookupElement): Boolean {
        var current: LookupElement? = element
        while (current != null) {
            if (current is LiveTemplateLookupElement) return true
            current = (current as? LookupElementDecorator<*>)?.delegate
        }
        return false
    }
}
