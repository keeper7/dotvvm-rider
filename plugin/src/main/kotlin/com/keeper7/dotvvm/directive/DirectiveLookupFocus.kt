package com.keeper7.dotvvm.directive

import com.intellij.codeInsight.lookup.Lookup
import com.intellij.codeInsight.lookup.LookupListener
import com.intellij.codeInsight.lookup.LookupManagerListener
import com.intellij.codeInsight.lookup.impl.LookupImpl
import com.intellij.psi.PsiFile
import com.keeper7.dotvvm.lang.DotControlFileType
import com.keeper7.dotvvm.lang.DotHtmlFileType
import com.keeper7.dotvvm.lang.DotMasterFileType

/**
 * Selects an item in the popup that opens in a file's header, so that Tab inserts it.
 *
 * An auto-popup the platform is unsure about opens with **no item selected** — `SEMI_FOCUSED`,
 * `currentItem == null`. The list is on screen, but Tab has nothing to insert. On a directive's
 * name it typed a tab character; in a directive's *value* it was worse — with `@masterPage Vi`
 * the tab reached Emmet instead and expanded the text into `<Vi></Vi>`, markup in the middle of
 * a header. Measured: the focus degree alone changes nothing, the selection alone is enough.
 *
 * The selection cannot be made when the lookup appears, because it holds no items yet — hence
 * the listener within a listener. Only an absent selection is filled in, so arrowing away is
 * never undone. Restricted to the header of a DotVVM file; everywhere else the platform's own
 * judgement stands, and inside a tag it selects an item by itself.
 */
class DirectiveLookupFocus : LookupManagerListener {

    override fun activeLookupChanged(oldLookup: Lookup?, newLookup: Lookup?) {
        val lookup = newLookup as? LookupImpl ?: return
        if (!isInDirectiveArea(lookup)) return

        lookup.addLookupListener(object : LookupListener {
            override fun uiRefreshed() {
                if (lookup.currentItem != null) return
                lookup.currentItem = lookup.items.firstOrNull() ?: return
            }
        })
    }

    private fun isInDirectiveArea(lookup: LookupImpl): Boolean {
        val file = lookup.psiFile ?: return false
        return isInHeader(file, lookup.editor.caretModel.offset)
    }

    /**
     * Whether the caret is in the header at all — on a directive's name or in its value. Both
     * halves need the selection: the names come from the plugin, the values from the server,
     * and neither gets one from the platform.
     */
    fun isInHeader(file: PsiFile, offset: Int): Boolean {
        val fileType = file.viewProvider.virtualFile?.fileType
        if (fileType != DotHtmlFileType.INSTANCE &&
            fileType != DotControlFileType.INSTANCE &&
            fileType != DotMasterFileType.INSTANCE) return false

        val before = file.text.take(offset.coerceIn(0, file.text.length))

        // Past the first tag the body has begun and the platform knows what it is looking at
        if (before.lineSequence().any { it.trimStart().startsWith('<') }) return false

        return before.lineSequence().last().trimStart().startsWith('@') ||
               DirectiveScanner.isOnName(file.text, offset)
    }
}
