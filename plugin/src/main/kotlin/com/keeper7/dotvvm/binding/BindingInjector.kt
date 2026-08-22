package com.keeper7.dotvvm.binding

import com.intellij.lang.injection.MultiHostInjector
import com.intellij.lang.injection.MultiHostRegistrar
import com.intellij.openapi.util.TextRange
import com.intellij.psi.PsiElement
import com.intellij.psi.PsiLanguageInjectionHost
import com.intellij.psi.util.PsiTreeUtil
import com.intellij.psi.xml.XmlAttributeValue
import com.intellij.psi.xml.XmlComment
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

        // Text range inside the host (for an attribute, without the quotes)
        val inner = when (context) {
            is XmlAttributeValue ->
                TextRange(context.valueTextRange.startOffset - context.textRange.startOffset,
                          context.valueTextRange.endOffset - context.textRange.startOffset)
            else -> TextRange(0, context.textLength)
        }
        if (inner.isEmpty) return

        val text = context.text.substring(inner.startOffset, inner.endOffset)
        val commented = commentRangesIn(context)
        val places = BindingScanner.scan(text)
            .map { TextRange(inner.startOffset + it.start, inner.startOffset + it.end) }
            .filterNot { place -> commented.any { it.intersects(place) } }
        if (places.isEmpty()) return

        // One injected file per host, with several places inside it
        registrar.startInjecting(BindingLanguage.INSTANCE)
        for (place in places) {
            registrar.addPlace(null, null, context, place)
        }
        registrar.doneInjecting()
    }

    /**
     * The comments inside the host, in the host's own coordinates.
     *
     * A server-side comment ends up as an `XmlComment` **inside** an `XmlText`, so the host we
     * are injecting into spans it. Without this a commented-out binding would still be treated
     * as code — highlighted, resolved and navigable, in text that never reaches the browser.
     */
    private fun commentRangesIn(context: PsiElement): List<TextRange> {
        val start = context.textRange.startOffset
        return PsiTreeUtil.findChildrenOfType(context, XmlComment::class.java)
            .map { it.textRange.shiftLeft(start) }
    }

    private fun isDotvvmFile(context: PsiElement): Boolean {
        val fileType = context.containingFile?.viewProvider?.virtualFile?.fileType ?: return false
        return fileType == DotHtmlFileType.INSTANCE ||
               fileType == DotControlFileType.INSTANCE ||
               fileType == DotMasterFileType.INSTANCE
    }
}
