package com.keeper7.dotvvm.binding

import com.intellij.codeInsight.editorActions.TypedHandlerDelegate
import com.intellij.openapi.editor.Editor
import com.intellij.openapi.project.Project
import com.intellij.psi.PsiFile
import com.keeper7.dotvvm.lang.DotControlFileType
import com.keeper7.dotvvm.lang.DotHtmlFileType
import com.keeper7.dotvvm.lang.DotMasterFileType

/**
 * Closes a binding the moment it is opened: `{{` becomes `{{}}` with the caret between them.
 *
 * The platform closes brackets for the languages it knows, and a DotVVM binding is not one of
 * them — the file is HTML, where a brace is ordinary text. Writing the pair by hand is the sort
 * of thing an editor is for, and getting it wrong is worse than not doing it at all, so
 * [ClosingBraces] refuses wherever something already closes the binding.
 *
 * The caret is put back where it was: it belongs inside the braces, which is also where the
 * completion popup is about to be asked for.
 */
class BindingBraceHandler : TypedHandlerDelegate() {

    override fun charTyped(c: Char, project: Project, editor: Editor, file: PsiFile): Result {
        if (c != '{') return Result.CONTINUE

        val fileType = file.viewProvider.virtualFile?.fileType
        if (fileType != DotHtmlFileType.INSTANCE &&
            fileType != DotControlFileType.INSTANCE &&
            fileType != DotMasterFileType.INSTANCE) return Result.CONTINUE

        val offset = editor.caretModel.offset
        if (!ClosingBraces.needed(editor.document.charsSequence, offset)) return Result.CONTINUE

        editor.document.insertString(offset, "}}")
        editor.caretModel.moveToOffset(offset)

        // The popup still has to open, and that is another handler's business
        return Result.CONTINUE
    }
}
