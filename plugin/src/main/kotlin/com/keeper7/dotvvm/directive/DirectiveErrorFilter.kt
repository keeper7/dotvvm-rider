package com.keeper7.dotvvm.directive

import com.intellij.codeInsight.highlighting.HighlightErrorFilter
import com.intellij.psi.PsiErrorElement
import com.keeper7.dotvvm.lang.DotControlFileType
import com.keeper7.dotvvm.lang.DotHtmlFileType
import com.keeper7.dotvvm.lang.DotMasterFileType

/**
 * Skryje chybu, kterou HTML parser hlásí kvůli direktivám v hlavičce souboru.
 *
 * HTML nepřipouští `<!DOCTYPE>` po textu, a direktivy jsou pro parser text — proto na
 * DOCTYPE ukáže `Unexpected tokens`, ačkoli soubor je správně. Naučit parser direktivy
 * se ukázalo jako horší lék než nemoc: vsunutím vlastního uzlu před `XML_PROLOG` ztratí
 * platforma HTML schéma a začne hlásit jako neznámé i `<html>` a `<div>`. Filtrovat
 * hlášku je proto jediný zásah, který nechá strom dokumentu na pokoji.
 *
 * Filtr je záměrně úzký: mlčí jen o chybách v hlavičce souboru, které stojí a padají
 * s přítomností direktiv. Chyby v těle dokumentu prochází beze změny.
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

        // Chyba uvnitř samotného direktivového bloku
        if (start < directives.last().end) return false

        // Chyba na DOCTYPE, který za direktivami následuje — jediné, co parseru vadí
        val doctype = text.indexOf("<!DOCTYPE", directives.last().end, ignoreCase = true)
        return !(doctype >= 0 && element.textRange.containsOffset(doctype))
    }
}
