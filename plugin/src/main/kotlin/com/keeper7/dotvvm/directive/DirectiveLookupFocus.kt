package com.keeper7.dotvvm.directive

import com.intellij.codeInsight.lookup.Lookup
import com.intellij.codeInsight.lookup.LookupListener
import com.intellij.codeInsight.lookup.LookupManagerListener
import com.intellij.codeInsight.lookup.impl.LookupImpl
import com.keeper7.dotvvm.lang.DotControlFileType
import com.keeper7.dotvvm.lang.DotHtmlFileType
import com.keeper7.dotvvm.lang.DotMasterFileType

/**
 * Selects an item in the popup that opens on `@`, so that Tab inserts the directive.
 *
 * An auto-popup the platform is unsure about opens with **no item selected** — `SEMI_FOCUSED`,
 * `currentItem == null`. The list is on screen, but Tab has nothing to insert and types a tab
 * character instead; only an arrow key picks an item. Measured: the focus degree alone changes
 * nothing, the selection alone is enough.
 *
 * The selection cannot be made when the lookup appears, because it holds no items yet — hence
 * the listener within a listener. Only an absent selection is filled in, so arrowing away is
 * never undone. Restricted to the header of a DotVVM file; everywhere else the platform's own
 * judgement stands.
 */
class DirectiveLookupFocus : LookupManagerListener {

    override fun activeLookupChanged(oldLookup: Lookup?, newLookup: Lookup?) {
        val lookup = newLookup as? LookupImpl ?: return
        if (!isDirectiveName(lookup)) return

        lookup.addLookupListener(object : LookupListener {
            override fun uiRefreshed() {
                if (lookup.currentItem != null) return
                lookup.currentItem = lookup.items.firstOrNull() ?: return
            }
        })
    }

    private fun isDirectiveName(lookup: LookupImpl): Boolean {
        val file = lookup.psiFile ?: return false
        val fileType = file.viewProvider.virtualFile.fileType
        if (fileType != DotHtmlFileType.INSTANCE &&
            fileType != DotControlFileType.INSTANCE &&
            fileType != DotMasterFileType.INSTANCE) return false

        val offset = lookup.editor.caretModel.offset
        return DirectiveScanner.isOnName(file.text, offset) && startsWithAtSign(file.text, offset)
    }

    /** Whether the word being typed starts with an at sign. */
    private fun startsWithAtSign(text: String, offset: Int): Boolean {
        var i = offset.coerceIn(0, text.length)
        while (i > 0 && text[i - 1].isLetter()) i--
        return i > 0 && text[i - 1] == '@'
    }
}
