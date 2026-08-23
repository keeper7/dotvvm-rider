package com.keeper7.dotvvm.navigation

import com.intellij.codeInsight.navigation.actions.GotoDeclarationHandler
import com.intellij.openapi.editor.Editor
import com.intellij.psi.PsiElement
import com.intellij.psi.PsiFile
import com.intellij.psi.PsiManager
import com.intellij.psi.xml.XmlTag
import com.intellij.psi.xml.XmlTokenType
import com.keeper7.dotvvm.lang.DotControlFileType
import com.keeper7.dotvvm.lang.DotHtmlFileType
import com.keeper7.dotvvm.lang.DotMasterFileType
import com.keeper7.dotvvm.lsp.ControlRegistrations

/**
 * Navigation out of a control tag: `<cc:MyControl>` leads to the .dotcontrol file registered
 * for it, `<dot:Button>` to the source declaring the class.
 *
 * This cannot be left to the LSP server, although the server has the registry and answers
 * `textDocument/definition`. The platform routes an LSP definition through
 * `psi.implicitReferenceProvider`, and *implicit* means it asks only where the element carries
 * no reference of its own — an `XmlTag` always carries one, resolving to its own name. That
 * self-reference is what underlines the tag and then goes nowhere. A `GotoDeclarationHandler`
 * is asked regardless, which is why the directives are handled the same way.
 */
class ControlNavigationHandler : GotoDeclarationHandler {

    override fun getGotoDeclarationTargets(
        sourceElement: PsiElement?,
        offset: Int,
        editor: Editor?
    ): Array<PsiElement>? {
        val element = sourceElement ?: return null
        val file = element.containingFile ?: return null
        val fileType = file.viewProvider.virtualFile.fileType
        if (fileType != DotHtmlFileType.INSTANCE &&
            fileType != DotControlFileType.INSTANCE &&
            fileType != DotMasterFileType.INSTANCE) return null

        // The token itself, not XmlTag.name: an HTML tag reports its name lower-cased, and
        // `cc:mycontrol` matches no registration. The token holds what the author wrote.
        if (element.node?.elementType != XmlTokenType.XML_NAME) return null
        if (element.parent !is XmlTag) return null

        val name = element.text
        val prefix = name.substringBefore(':', missingDelimiterValue = "")
        val tagName = name.substringAfter(':')
        if (prefix.isEmpty() || tagName.isEmpty()) return null

        val target = resolve(file, prefix, tagName) ?: return null
        return arrayOf(target)
    }

    private fun resolve(file: PsiFile, prefix: String, tagName: String): PsiFile? {
        val registrations = ControlRegistrations.of(file.project)

        registrations.markupControl(prefix, tagName)?.src?.let { src ->
            // DotVVM registers a few of its own controls under embedded://, which names a
            // resource inside an assembly and no file on disk
            if (src.startsWith("embedded://")) return null

            val virtual = ProjectFiles.resolve(file, src) ?: return null
            return PsiManager.getInstance(file.project).findFile(virtual)
        }

        return registrations.namespaces(prefix)
            .firstNotNullOfOrNull {
                ProjectFiles.findTypeSource(file.project, "${it.namespace}.$tagName")
            }
    }
}
