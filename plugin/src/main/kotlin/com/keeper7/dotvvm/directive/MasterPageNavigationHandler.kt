package com.keeper7.dotvvm.directive

import com.intellij.codeInsight.navigation.actions.GotoDeclarationHandler
import com.intellij.openapi.editor.Editor
import com.intellij.openapi.roots.ProjectRootManager
import com.intellij.psi.PsiElement
import com.intellij.psi.PsiManager
import com.keeper7.dotvvm.lang.DotControlFileType
import com.keeper7.dotvvm.lang.DotHtmlFileType
import com.keeper7.dotvvm.lang.DotMasterFileType

/**
 * Navigation from `@masterPage` to the file it references. The path is relative to the project
 * root, the same way DotVVM reads it at run time.
 *
 * Directives pointing at a .NET type (`@viewModel`, `@baseType`) do not belong here: only the
 * LSP server can resolve those, since it has the control registry and the compiled assembly.
 */
class MasterPageNavigationHandler : GotoDeclarationHandler {

    /** The directives whose value is a path. `js` is the view module one; `viewModule` is not
     *  a DotVVM directive at all, and navigating from it was dead code. */
    private val fileDirectives = setOf("masterPage", "js")

    override fun getGotoDeclarationTargets(
        sourceElement: PsiElement?,
        offset: Int,
        editor: Editor?
    ): Array<PsiElement>? {
        val file = sourceElement?.containingFile ?: return null
        val fileType = file.viewProvider.virtualFile.fileType
        if (fileType != DotHtmlFileType.INSTANCE &&
            fileType != DotControlFileType.INSTANCE &&
            fileType != DotMasterFileType.INSTANCE) return null

        val directive = DirectiveScanner.scan(file.text).firstOrNull {
            it.name in fileDirectives && offset >= it.end - it.value.length && offset <= it.end
        } ?: return null
        if (directive.value.isEmpty()) return null

        // The path is relative to a content root. Projects with several roots are rare, but
        // walking all of them is cheap and avoids having to guess which root is the right one.
        val target = ProjectRootManager.getInstance(file.project).contentRoots
            .firstNotNullOfOrNull { it.findFileByRelativePath(directive.value) } ?: return null
        val psi = PsiManager.getInstance(file.project).findFile(target) ?: return null
        return arrayOf(psi)
    }
}
