package com.keeper7.dotvvm.directive

import com.intellij.codeInsight.navigation.actions.GotoDeclarationHandler
import com.intellij.openapi.editor.Editor
import com.intellij.psi.PsiElement
import com.intellij.psi.PsiManager
import com.keeper7.dotvvm.lang.DotControlFileType
import com.keeper7.dotvvm.lang.DotHtmlFileType
import com.keeper7.dotvvm.lang.DotMasterFileType
import com.keeper7.dotvvm.navigation.ProjectFiles

/**
 * Navigation out of a directive: to the file `@masterPage` names, and to the source of the type
 * `@viewModel` or `@baseType` names. The path is relative to the project root, the same way
 * DotVVM reads it at run time.
 *
 * The type half is done here rather than left to the server, although the server answers
 * `textDocument/definition` correctly. A directive is not markup: the PSI holds it as bare
 * `XML_DATA_CHARACTERS` directly under the document, not even wrapped in `XmlText`, and on such
 * a position the platform never asks the LSP client at all — the link was not even underlined.
 * Finding the file is a filesystem search either way, and the plugin has the project index.
 *
 * Directives pointing at a .NET type (`@viewModel`, `@baseType`) do not belong here: only the
 * LSP server can resolve those, since it has the control registry and the compiled assembly.
 */
class MasterPageNavigationHandler : GotoDeclarationHandler {

    /** The directives whose value is a path. `js` is the view module one; `viewModule` is not
     *  a DotVVM directive at all, and navigating from it was dead code. */
    private val fileDirectives = setOf("masterPage", "js")

    /** The directives whose value is a .NET type. */
    private val typeDirectives = setOf("viewModel", "baseType")

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
            it.name in fileDirectives + typeDirectives &&
                offset >= it.end - it.value.length && offset <= it.end
        } ?: return null
        if (directive.value.isEmpty()) return null

        if (directive.name in typeDirectives) {
            // The value has two halves and they lead to different places. The caret decides
            // which: on the assembly it used to jump to the type's source, which is the one
            // file the reader was demonstrably not asking about.
            val valueStart = directive.end - directive.value.length
            val comma = directive.value.indexOf(',')
            val onAssembly = comma >= 0 && offset > valueStart + comma

            val target =
                if (onAssembly) ProjectFiles.findAssemblyProject(
                    file.project, directive.value.substring(comma + 1))
                else ProjectFiles.findTypeSource(file.project, directive.value)
            return target?.let { arrayOf(it) }
        }

        // The path is relative to the **DotVVM project's** root, which is the nearest directory
        // upwards holding a .csproj — not to a content root of the IDE. The two differ whenever
        // the opened project is larger than the web app: with the whole repository open,
        // `Views/Site.dotmaster` resolved against the repository root, where nothing of the
        // sort exists. The server has always read it this way.
        val target = ProjectFiles.resolve(file, directive.value) ?: return null
        val psi = PsiManager.getInstance(file.project).findFile(target) ?: return null
        return arrayOf(psi)
    }

}
