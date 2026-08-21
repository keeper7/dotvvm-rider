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
 * Skok z `@masterPage` na odkazovaný soubor. Cesta je relativní ke kořeni projektu,
 * stejně jako ji chápe DotVVM za běhu.
 *
 * Direktivy, které ukazují na .NET typ (`@viewModel`, `@baseType`), sem nepatří —
 * ty umí rozřešit jen LSP server, který má registr kontrolek a sestavenou assembly.
 */
class MasterPageNavigationHandler : GotoDeclarationHandler {

    private val fileDirectives = setOf("masterPage", "js", "viewModule")

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

        // Cesta je relativní ke kořeni obsahu; projektů s více kořeny se to netýká často,
        // ale procházet je všechny je levné a nespoléhá to na odhad kořene.
        val target = ProjectRootManager.getInstance(file.project).contentRoots
            .firstNotNullOfOrNull { it.findFileByRelativePath(directive.value) } ?: return null
        val psi = PsiManager.getInstance(file.project).findFile(target) ?: return null
        return arrayOf(psi)
    }
}
