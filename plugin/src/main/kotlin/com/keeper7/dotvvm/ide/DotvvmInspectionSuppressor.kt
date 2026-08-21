package com.keeper7.dotvvm.ide

import com.intellij.codeInspection.InspectionSuppressor
import com.intellij.codeInspection.SuppressQuickFix
import com.intellij.psi.PsiElement

/**
 * Umlčí platformní inspekci nevázaného XML prefixu. V DotVVM se prefixy nedeklarují
 * v souboru přes `xmlns:`, ale registrují v `DotvvmStartup.cs` — inspekce se ptá souboru,
 * takže by podtrhla každou kontrolku. Zda prefix opravdu existuje, ví jen LSP server,
 * a ten to hlásí vlastní diagnostikou.
 */
class DotvvmInspectionSuppressor : InspectionSuppressor {

    override fun isSuppressedFor(element: PsiElement, toolId: String): Boolean =
        toolId == UNBOUND_PREFIX

    override fun getSuppressActions(element: PsiElement?, toolId: String): Array<SuppressQuickFix> =
        SuppressQuickFix.EMPTY_ARRAY

    private companion object {
        const val UNBOUND_PREFIX = "XmlUnboundNsPrefix"
    }
}
