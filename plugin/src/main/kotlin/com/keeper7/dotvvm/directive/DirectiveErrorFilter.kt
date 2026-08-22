package com.keeper7.dotvvm.directive

import com.intellij.codeInsight.highlighting.HighlightErrorFilter
import com.intellij.psi.PsiErrorElement
import com.keeper7.dotvvm.lang.DotControlFileType
import com.keeper7.dotvvm.lang.DotHtmlFileType
import com.keeper7.dotvvm.lang.DotMasterFileType

/**
 * Hides the error the HTML parser reports because of the directives in the file header.
 *
 * HTML does not allow `<!DOCTYPE>` after text, and to the parser directives are text — hence
 * `Unexpected tokens` on the DOCTYPE even though the file is correct. Teaching the parser about
 * directives proved worse than the disease: inserting a node of our own before `XML_PROLOG`
 * costs the platform its HTML schema, and it starts reporting even `<html>` and `<div>` as
 * unknown. Filtering the message is therefore the only fix that leaves the document tree alone.
 *
 * The filter is deliberately narrow: it stays silent only about header errors that exist
 * because of the directives. Errors in the document body pass through unchanged.
 */
class DirectiveErrorFilter : HighlightErrorFilter() {

    override fun shouldHighlightErrorElement(element: PsiErrorElement): Boolean {
        val file = element.containingFile ?: return true
        val fileType = file.viewProvider.virtualFile.fileType
        if (fileType != DotHtmlFileType.INSTANCE &&
            fileType != DotControlFileType.INSTANCE &&
            fileType != DotMasterFileType.INSTANCE) return true

        val text = file.text
        val directives = DirectiveScanner.scan(text)
        if (directives.isEmpty()) return true

        val start = element.textRange.startOffset

        // An error inside the directive block itself
        if (start < directives.last().end) return false

        // An error on the DOCTYPE that follows the directives: the only thing the parser minds
        val doctype = text.indexOf("<!DOCTYPE", directives.last().end, ignoreCase = true)
        return !(doctype >= 0 && element.textRange.containsOffset(doctype))
    }
}
