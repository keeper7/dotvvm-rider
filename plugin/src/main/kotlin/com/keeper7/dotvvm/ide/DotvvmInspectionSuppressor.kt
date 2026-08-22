package com.keeper7.dotvvm.ide

import com.intellij.codeInspection.InspectionSuppressor
import com.intellij.codeInspection.SuppressQuickFix
import com.intellij.psi.PsiElement
import com.intellij.psi.util.PsiTreeUtil
import com.intellij.psi.xml.XmlAttribute
import com.intellij.psi.xml.XmlTag

/**
 * Silences the platform inspections that cannot know anything about DotVVM.
 *
 * A control prefix is not declared in the file with `xmlns:` but registered
 * v `DotvvmStartup.cs`, a properties jako `Visible` nebo `Class-required` jsou DotVVM
 * extensions of HTML elements. Both ask the file, so they would underline correct code. What
 * really exists in the project is known only to the LSP server, which reports it itself.
 */
class DotvvmInspectionSuppressor : InspectionSuppressor {

    override fun isSuppressedFor(element: PsiElement, toolId: String): Boolean = when (toolId) {
        UNBOUND_PREFIX -> true
        UNKNOWN_ATTRIBUTE -> isDotvvmAttribute(element)
        else -> false
    }

    override fun getSuppressActions(element: PsiElement?, toolId: String): Array<SuppressQuickFix> =
        SuppressQuickFix.EMPTY_ARRAY

    /**
     * Decides narrowly: it stays silent only about attributes DotVVM actually introduces. A
     * typo in an ordinary HTML attribute (`clas`) must still be reported, or the suppression
     * would do more harm than good.
     */
    private fun isDotvvmAttribute(element: PsiElement): Boolean {
        val attribute = element as? XmlAttribute
            ?: PsiTreeUtil.getParentOfType(element, XmlAttribute::class.java)
            ?: return false

        // On a prefixed control every attribute is one of its properties
        val tag = PsiTreeUtil.getParentOfType(attribute, XmlTag::class.java)
        if (tag != null && tag.name.contains(':')) return true

        val name = attribute.name
        return name in HTML_EXTENSIONS || PREFIXES.any { name.startsWith(it) }
    }

    private companion object {
        const val UNBOUND_PREFIX = "XmlUnboundNsPrefix"
        const val UNKNOWN_ATTRIBUTE = "HtmlUnknownAttribute"

        /** Properties of `HtmlGenericControl`: DotVVM properties written on an HTML element. */
        val HTML_EXTENSIONS = setOf(
            "Visible", "DataContext", "IncludeInPage", "InnerText"
        )

        /**
         * The other two kinds of properties, recognisable by their spelling: a dot marks an
         * *attached property* (`Validator.Value` — property `Value` attached by the `Validator`
         * class), a dash marks a *property group* (`Class-required` — group `Class-`, key
         * `required`). They are properties just as much as the ones above; they differ only in
         * where they are declared.
         */
        val PREFIXES = listOf(
            "Validator.", "Validation.", "Events.", "PostBack.", "RenderSettings.",
            "Class-", "Style-", "Attr-", "Property-"
        )
    }
}
