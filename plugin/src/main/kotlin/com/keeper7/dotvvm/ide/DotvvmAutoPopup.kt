package com.keeper7.dotvvm.ide

import com.intellij.codeInsight.AutoPopupController
import com.intellij.codeInsight.editorActions.TypedHandlerDelegate
import com.intellij.openapi.editor.Editor
import com.intellij.openapi.project.Project
import com.intellij.psi.PsiFile
import com.keeper7.dotvvm.binding.BindingLocation
import com.keeper7.dotvvm.directive.isInDirectiveArea
import com.keeper7.dotvvm.lang.DotControlFileType
import com.keeper7.dotvvm.lang.DotHtmlFileType
import com.keeper7.dotvvm.lang.DotMasterFileType

/**
 * Opens the completion popup on the character that begins a directive or a binding.
 *
 * `CompletionContributor.invokeAutoPopup` did this in both places until the platform deprecated
 * it — reported by Marketplace's verifier on 0.4.0, twice. A typed handler is the supported way
 * and suits the job better: the editor arrives as an argument instead of being fetched from
 * `FileEditorManager`, which is what the binding half had to do.
 *
 * **The decision is left to the condition rather than made here**, because that condition is
 * evaluated on a *committed* file. Judged in `charTyped`, the PSI can still be the one from
 * before the character was typed — and for the brace that is not academic:
 * [BindingBraceHandler][com.keeper7.dotvvm.binding.BindingBraceHandler] writes the closing `}}`
 * in the same round, and the caret only stands inside a binding once it has.
 *
 * The handler runs for every file the IDE opens, so the file type is what keeps it out of the
 * way of the rest of them.
 */
class DotvvmAutoPopup : TypedHandlerDelegate() {

    override fun charTyped(c: Char, project: Project, editor: Editor, file: PsiFile): Result {
        if (c != AT && c != BRACE) return Result.CONTINUE

        val fileType = file.viewProvider.virtualFile.fileType
        if (fileType != DotHtmlFileType.INSTANCE &&
            fileType != DotControlFileType.INSTANCE &&
            fileType != DotMasterFileType.INSTANCE) return Result.CONTINUE

        AutoPopupController.getInstance(project).scheduleAutoPopup(editor) { committed ->
            wantsPopup(c, committed, editor.caretModel.offset)
        }
        return Result.CONTINUE
    }

    /**
     * The directive half reads the text and the binding half the PSI, which is why the binding
     * one goes through [BindingLocation]: a finished binding is injected, and the offsets of the
     * injected fragment are not the host's.
     */
    private fun wantsPopup(c: Char, file: PsiFile, caret: Int): Boolean = when (c) {
        AT -> isInDirectiveArea(file.text, caret)
        else -> BindingLocation.at(file, caret) != null
    }

    private companion object {
        const val AT = '@'
        const val BRACE = '{'
    }
}
