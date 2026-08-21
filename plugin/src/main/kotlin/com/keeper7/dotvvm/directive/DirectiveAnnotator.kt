package com.keeper7.dotvvm.directive

import com.intellij.lang.annotation.AnnotationHolder
import com.intellij.lang.annotation.Annotator
import com.intellij.lang.annotation.HighlightSeverity
import com.intellij.openapi.editor.DefaultLanguageHighlighterColors as Default
import com.intellij.openapi.editor.colors.TextAttributesKey.createTextAttributesKey
import com.intellij.openapi.util.TextRange
import com.intellij.psi.PsiElement
import com.intellij.psi.PsiFile
import com.keeper7.dotvvm.lang.DotControlFileType
import com.keeper7.dotvvm.lang.DotHtmlFileType
import com.keeper7.dotvvm.lang.DotMasterFileType

object DirectiveColors {
    val NAME  = createTextAttributesKey("DOTVVM_DIRECTIVE_NAME", Default.KEYWORD)
    val VALUE = createTextAttributesKey("DOTVVM_DIRECTIVE_VALUE", Default.INSTANCE_FIELD)
}

/**
 * Obarví direktivy v hlavičce souboru. Anotuje se celý soubor najednou, protože direktivy
 * nemají vlastní PSI uzel — parser je vidí jako obyčejný text (viz úkol 2b).
 */
class DirectiveAnnotator : Annotator {

    override fun annotate(element: PsiElement, holder: AnnotationHolder) {
        if (element !is PsiFile) return

        val fileType = element.viewProvider.virtualFile.fileType
        if (fileType != DotHtmlFileType.INSTANCE &&
            fileType != DotControlFileType.INSTANCE &&
            fileType != DotMasterFileType.INSTANCE) return

        for (directive in DirectiveScanner.scan(element.text)) {
            val nameEnd = directive.start + directive.name.length + 1
            holder.newSilentAnnotation(HighlightSeverity.INFORMATION)
                .range(TextRange(directive.start, nameEnd))
                .textAttributes(DirectiveColors.NAME)
                .create()

            if (directive.value.isNotEmpty()) {
                val valueStart = directive.end - directive.value.length
                holder.newSilentAnnotation(HighlightSeverity.INFORMATION)
                    .range(TextRange(valueStart, directive.end))
                    .textAttributes(DirectiveColors.VALUE)
                    .create()
            }
        }
    }
}
