package com.keeper7.dotvvm.ide

import com.intellij.codeInsight.lookup.Lookup
import com.intellij.codeInsight.lookup.LookupListener
import com.intellij.codeInsight.lookup.LookupManagerListener
import com.intellij.codeInsight.lookup.impl.LookupImpl
import com.intellij.psi.PsiFile
import com.keeper7.dotvvm.binding.BindingLocation
import com.keeper7.dotvvm.directive.DirectiveScanner
import com.keeper7.dotvvm.lang.DotControlFileType
import com.keeper7.dotvvm.lang.DotHtmlFileType
import com.keeper7.dotvvm.lang.DotMasterFileType

/**
 * Selects an item in a popup the platform left unselected, so that Tab inserts it.
 *
 * An auto-popup the platform is unsure about opens with **no item selected** — `SEMI_FOCUSED`,
 * `currentItem == null`. The list is on screen, but Tab has nothing to insert, so it reaches
 * whatever is behind it. In a file's header that was a tab character on a directive's name and,
 * worse, Emmet in a directive's value: `@masterPage Vi` + Tab became `<Vi></Vi>`. Inside a
 * binding it is Emmet again — `{{re` + Tab became `<re></re>` instead of `resource:`.
 * Measured: the focus degree alone changes nothing, the selection alone is enough.
 *
 * The selection cannot be made when the lookup appears, because it holds no items yet — hence
 * the listener within a listener. Only an absent selection is filled in, so arrowing away is
 * never undone. Restricted to the two places where the platform leaves one out; inside a tag it
 * selects an item by itself.
 */
class DotvvmLookupFocus : LookupManagerListener {

    override fun activeLookupChanged(oldLookup: Lookup?, newLookup: Lookup?) {
        val lookup = newLookup as? LookupImpl ?: return

        val file = lookup.psiFile ?: return
        val offset = lookup.editor.caretModel.offset
        if (!isInHeader(file, offset) && !isInBinding(file, offset)) return

        lookup.addLookupListener(object : LookupListener {
            override fun uiRefreshed() {
                if (lookup.currentItem != null) return
                lookup.currentItem = lookup.items.firstOrNull() ?: return
            }
        })
    }

    /**
     * Whether the caret is in the header at all — on a directive's name or in its value. Both
     * halves need the selection: the names come from the plugin, the values from the server,
     * and neither gets one from the platform.
     */
    fun isInHeader(file: PsiFile, offset: Int): Boolean {
        if (!isDotvvmFile(file)) return false

        val before = file.text.take(offset.coerceIn(0, file.text.length))

        // Past the first tag the body has begun and the platform knows what it is looking at
        if (before.lineSequence().any { it.trimStart().startsWith('<') }) return false

        return before.lineSequence().last().trimStart().startsWith('@') ||
               DirectiveScanner.isOnName(file.text, offset)
    }

    /** Whether the caret is inside a binding expression, where the same thing happens. */
    fun isInBinding(file: PsiFile, offset: Int): Boolean = BindingLocation.at(file, offset) != null

    private fun isDotvvmFile(file: PsiFile): Boolean {
        val fileType = file.viewProvider.virtualFile?.fileType
        return fileType == DotHtmlFileType.INSTANCE ||
               fileType == DotControlFileType.INSTANCE ||
               fileType == DotMasterFileType.INSTANCE
    }
}
