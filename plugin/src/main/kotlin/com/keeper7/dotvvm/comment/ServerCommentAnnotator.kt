package com.keeper7.dotvvm.comment

import com.intellij.lang.annotation.AnnotationHolder
import com.intellij.lang.annotation.Annotator
import com.intellij.lang.annotation.HighlightSeverity
import com.intellij.openapi.editor.XmlHighlighterColors
import com.intellij.openapi.editor.colors.TextAttributesKey.createTextAttributesKey
import com.intellij.openapi.util.TextRange
import com.intellij.psi.PsiElement
import com.intellij.psi.PsiFile
import com.keeper7.dotvvm.lang.DotControlFileType
import com.keeper7.dotvvm.lang.DotHtmlFileType
import com.keeper7.dotvvm.lang.DotMasterFileType

object ServerCommentColors {
    val IN_TAG = createTextAttributesKey("DOTVVM_COMMENT_IN_TAG", XmlHighlighterColors.HTML_COMMENT)
}

/**
 * Paints the server-side comments that sit between a tag's attributes.
 *
 * Everywhere else the lexer does it: `ServerCommentMasker` turns the comment into an HTML one
 * and the colour follows. Inside a tag that trick is not available — HTML has no comment there —
 * so the masker blanks the comment out instead, and what the lexer then sees is whitespace.
 * This puts the colour back, off text offsets, the same way `DirectiveAnnotator` does.
 */
class ServerCommentAnnotator : Annotator {

    override fun annotate(element: PsiElement, holder: AnnotationHolder) {
        if (element !is PsiFile) return

        val fileType = element.viewProvider.virtualFile.fileType
        if (fileType != DotHtmlFileType.INSTANCE &&
            fileType != DotControlFileType.INSTANCE &&
            fileType != DotMasterFileType.INSTANCE) return

        for (comment in ServerCommentMasker.scan(element.text)) {
            if (!comment.insideTag) continue          // outside a tag the lexer has it covered
            holder.newSilentAnnotation(HighlightSeverity.INFORMATION)
                .range(TextRange(comment.start, comment.end))
                .textAttributes(ServerCommentColors.IN_TAG)
                .create()
        }
    }
}
