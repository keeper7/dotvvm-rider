package com.keeper7.dotvvm.binding

import com.intellij.lang.injection.InjectedLanguageManager
import com.intellij.psi.PsiFile
import com.keeper7.dotvvm.lang.DotControlFileType
import com.keeper7.dotvvm.lang.DotHtmlFileType
import com.keeper7.dotvvm.lang.DotMasterFileType

/** Where the caret stands, and in which file — the host one, whatever it was asked about. */
data class BindingSpot(val place: BindingPlace, val fileName: String)

/**
 * [BindingPosition] read through the PSI, which is where the injection has to be undone.
 *
 * A binding that is **finished** is injected as `DotVVMBinding`, so completion inside one runs
 * against a file of its own whose text is the fragment alone — measured, the language at the
 * caret in `{{resource: |}}` is DotVVMBinding, while in `{{re|` it is still the file's own. The
 * offsets of the two do not match, so the walk has to start from the host's text or it begins
 * in the middle of the binding it is being asked about.
 */
object BindingLocation {

    fun at(file: PsiFile, offset: Int): BindingSpot? {
        val manager = InjectedLanguageManager.getInstance(file.project)
        val injected = manager.isInjectedFragment(file)

        val host = (if (injected) manager.getTopLevelFile(file) else file) ?: return null
        if (!isDotvvmFile(host)) return null

        val hostOffset = if (injected) manager.injectedToHost(file, offset) else offset
        val place = BindingPosition.at(host.text, hostOffset) ?: return null

        return BindingSpot(place, host.name)
    }

    private fun isDotvvmFile(file: PsiFile): Boolean {
        val fileType = file.viewProvider.virtualFile?.fileType
        return fileType == DotHtmlFileType.INSTANCE ||
               fileType == DotControlFileType.INSTANCE ||
               fileType == DotMasterFileType.INSTANCE
    }
}
