package com.keeper7.dotvvm.directive

import com.intellij.codeInsight.navigation.actions.GotoDeclarationHandler
import com.intellij.openapi.editor.Editor
import com.intellij.openapi.roots.ProjectRootManager
import com.intellij.psi.search.FilenameIndex
import com.intellij.psi.search.GlobalSearchScope
import com.intellij.openapi.vfs.VirtualFile
import com.intellij.psi.PsiElement
import com.intellij.psi.PsiFile
import com.intellij.psi.PsiManager
import com.keeper7.dotvvm.lang.DotControlFileType
import com.keeper7.dotvvm.lang.DotHtmlFileType
import com.keeper7.dotvvm.lang.DotMasterFileType

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
                if (onAssembly) findAssemblyProject(file, directive.value.substring(comma + 1))
                else findTypeSource(file, directive.value)
            return target?.let { arrayOf(it) }
        }

        // The path is relative to the **DotVVM project's** root, which is the nearest directory
        // upwards holding a .csproj — not to a content root of the IDE. The two differ whenever
        // the opened project is larger than the web app: with the whole repository open,
        // `Views/Site.dotmaster` resolved against the repository root, where nothing of the
        // sort exists. The server has always read it this way.
        val target = resolve(file, directive.value) ?: return null
        val psi = PsiManager.getInstance(file.project).findFile(target) ?: return null
        return arrayOf(psi)
    }

    /**
     * The project building that assembly. An assembly has no source of its own; the .csproj is
     * the nearest thing there is, and its name matches the assembly unless the project renames
     * it with <AssemblyName>, which nothing in reach does.
     */
    private fun findAssemblyProject(file: PsiFile, assembly: String): PsiFile? {
        val name = assembly.substringBefore(',').trim()
        if (name.isEmpty()) return null

        val candidates = FilenameIndex.getVirtualFilesByName(
            "$name.csproj", GlobalSearchScope.projectScope(file.project))
        return PsiManager.getInstance(file.project).findFile(candidates.firstOrNull() ?: return null)
    }

    /**
     * The file declaring the type, searched by the last segment of its name — a view model is
     * routinely named differently from the view, and the file is named after the class. Anything
     * after a comma is the assembly and not part of the name.
     */
    private fun findTypeSource(file: PsiFile, value: String): PsiFile? {
        val typeName = value.substringBefore(',').trim().substringBefore('<')
        val shortName = typeName.substringAfterLast('.')
        if (shortName.isEmpty()) return null

        val scope = GlobalSearchScope.projectScope(file.project)
        val candidates = FilenameIndex.getVirtualFilesByName("$shortName.cs", scope)
        val manager = PsiManager.getInstance(file.project)

        // The file named after the class is the usual case; when several match, the one that
        // really declares it wins
        val declaring = candidates.firstOrNull { candidate ->
            manager.findFile(candidate)?.text?.contains("class $shortName") == true
        }
        return manager.findFile(declaring ?: candidates.firstOrNull() ?: return null)
    }

    private fun resolve(file: PsiFile, path: String): VirtualFile? {
        val root = projectRootOf(file.viewProvider.virtualFile)
        root?.findFileByRelativePath(path)?.let { return it }

        // No .csproj anywhere above — a bare folder of views, say. The content roots are then
        // the best guess left, and walking all of them is cheap.
        return ProjectRootManager.getInstance(file.project).contentRoots
            .firstNotNullOfOrNull { it.findFileByRelativePath(path) }
    }

    /** The nearest directory upwards that holds a .csproj, the same rule the server follows. */
    private fun projectRootOf(file: VirtualFile?): VirtualFile? {
        var dir = file?.parent
        while (dir != null) {
            if (dir.children?.any { it.extension == "csproj" } == true) return dir
            dir = dir.parent
        }
        return null
    }
}
