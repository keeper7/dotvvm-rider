package com.keeper7.dotvvm.ide

import com.intellij.codeInspection.InspectionSuppressor
import com.intellij.codeInspection.SuppressQuickFix
import com.intellij.psi.PsiElement
import com.intellij.psi.util.PsiTreeUtil
import com.intellij.psi.xml.XmlAttribute
import com.intellij.psi.xml.XmlTag

/**
 * Umlčí platformní inspekce, které o DotVVM nemohou nic vědět.
 *
 * Prefix kontrolky se v DotVVM nedeklaruje v souboru přes `xmlns:`, ale registruje
 * v `DotvvmStartup.cs`, a properties jako `Visible` nebo `Class-required` jsou DotVVM
 * rozšíření HTML elementů. Obojí se ptá souboru, takže by podtrhlo správný kód. Co
 * v projektu opravdu existuje, ví jen LSP server — a ten to hlásí vlastní diagnostikou.
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
     * Rozhoduje úzce: mlčí jen o atributech, které DotVVM opravdu zavádí. Překlep
     * v běžném HTML atributu (`clas`) se hlásit musí dál, jinak by potlačení škodilo víc,
     * než pomáhá.
     */
    private fun isDotvvmAttribute(element: PsiElement): Boolean {
        val attribute = element as? XmlAttribute
            ?: PsiTreeUtil.getParentOfType(element, XmlAttribute::class.java)
            ?: return false

        // Na kontrolce s prefixem jsou všechny atributy jejími properties
        val tag = PsiTreeUtil.getParentOfType(attribute, XmlTag::class.java)
        if (tag != null && tag.name.contains(':')) return true

        val name = attribute.name
        return name in HTML_EXTENSIONS || PREFIXES.any { name.startsWith(it) }
    }

    private companion object {
        const val UNBOUND_PREFIX = "XmlUnboundNsPrefix"
        const val UNKNOWN_ATTRIBUTE = "HtmlUnknownAttribute"

        /** Properties `HtmlGenericControl`, tedy DotVVM properties psané přímo na HTML elementu. */
        val HTML_EXTENSIONS = setOf(
            "Visible", "DataContext", "IncludeInPage", "InnerText"
        )

        /**
         * Zbylé dva druhy properties, poznatelné podle zápisu:
         * tečka je *attached property* (`Validator.Value` — property `Value` připojená
         * třídou `Validator`), pomlčka je *property group* (`Class-required` — skupina
         * `Class-`, klíč `required`). Významově jde o properties stejně jako výše,
         * jen se liší tím, kde jsou deklarované.
         */
        val PREFIXES = listOf(
            "Validator.", "Validation.", "Events.", "PostBack.", "RenderSettings.",
            "Class-", "Style-", "Attr-", "Property-"
        )
    }
}
