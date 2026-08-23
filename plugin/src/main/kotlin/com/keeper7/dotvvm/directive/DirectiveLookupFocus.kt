package com.keeper7.dotvvm.directive

import com.intellij.codeInsight.lookup.Lookup
import com.intellij.codeInsight.lookup.LookupManagerListener
import com.intellij.codeInsight.lookup.impl.LookupImpl
import com.keeper7.dotvvm.lang.DotControlFileType
import com.keeper7.dotvvm.lang.DotHtmlFileType
import com.keeper7.dotvvm.lang.DotMasterFileType

/**
 * Focuses the popup that opens on `@`, so Tab and Enter insert the directive straight away.
 *
 * An auto-popup the platform is not sure about opens *unfocused*: the item is listed but not
 * selected, and only an arrow key gives it the focus that Tab needs. That default keeps
 * completion out of the way while code is being typed, but a directive name is the only thing
 * that can follow an at sign at the top of a DotVVM file, so there is nothing to get in the way
 * of.
 *
 * Restricted to the header of a DotVVM file; everywhere else the platform's own judgement stands.
 */
class DirectiveLookupFocus : LookupManagerListener {

    override fun activeLookupChanged(oldLookup: Lookup?, newLookup: Lookup?) {
        val lookup = newLookup as? LookupImpl ?: return

        val file = lookup.psiFile ?: return
        val fileType = file.viewProvider.virtualFile.fileType
        if (fileType != DotHtmlFileType.INSTANCE &&
            fileType != DotControlFileType.INSTANCE &&
            fileType != DotMasterFileType.INSTANCE) return

        val offset = lookup.editor.caretModel.offset
        if (!DirectiveScanner.isOnName(file.text, offset)) return
        if (!startsWithAtSign(file.text, offset)) return

        lookup.lookupFocusDegree = com.intellij.codeInsight.lookup.LookupFocusDegree.FOCUSED
    }

    /** Whether the word being typed starts with an at sign. */
    private fun startsWithAtSign(text: String, offset: Int): Boolean {
        var i = offset.coerceIn(0, text.length)
        while (i > 0 && text[i - 1].isLetter()) i--
        return i > 0 && text[i - 1] == '@'
    }
}
