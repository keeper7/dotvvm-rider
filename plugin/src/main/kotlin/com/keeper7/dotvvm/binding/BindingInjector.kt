package com.keeper7.dotvvm.binding

import com.intellij.lang.injection.MultiHostInjector
import com.intellij.lang.injection.MultiHostRegistrar
import com.intellij.openapi.util.TextRange
import com.intellij.psi.PsiElement
import com.intellij.psi.PsiLanguageInjectionHost
import com.intellij.psi.xml.XmlAttributeValue
import com.intellij.psi.xml.XmlText
import com.keeper7.dotvvm.lang.DotControlFileType
import com.keeper7.dotvvm.lang.DotHtmlFileType
import com.keeper7.dotvvm.lang.DotMasterFileType

class BindingInjector : MultiHostInjector {

    override fun elementsToInjectIn(): List<Class<out PsiElement>> =
        listOf(XmlAttributeValue::class.java, XmlText::class.java)

    override fun getLanguagesToInject(registrar: MultiHostRegistrar, context: PsiElement) {
        if (!isDotvvmFile(context)) return
        if (context !is PsiLanguageInjectionHost) return
        if (!context.isValidHost) return

        // Rozsah textu uvnitř hostitele (u atributu bez uvozovek)
        val inner = when (context) {
            is XmlAttributeValue ->
                TextRange(context.valueTextRange.startOffset - context.textRange.startOffset,
                          context.valueTextRange.endOffset - context.textRange.startOffset)
            else -> TextRange(0, context.textLength)
        }
        if (inner.isEmpty) return

        val text = context.text.substring(inner.startOffset, inner.endOffset)
        val matches = BindingScanner.scan(text)
        if (matches.isEmpty()) return

        // Jeden injektovaný soubor na hostitele, více míst uvnitř
        registrar.startInjecting(BindingLanguage.INSTANCE)
        for (m in matches) {
            registrar.addPlace(
                null, null, context,
                TextRange(inner.startOffset + m.start, inner.startOffset + m.end)
            )
        }
        registrar.doneInjecting()
    }

    private fun isDotvvmFile(context: PsiElement): Boolean {
        val fileType = context.containingFile?.viewProvider?.virtualFile?.fileType ?: return false
        return fileType == DotHtmlFileType.INSTANCE ||
               fileType == DotControlFileType.INSTANCE ||
               fileType == DotMasterFileType.INSTANCE
    }
}
