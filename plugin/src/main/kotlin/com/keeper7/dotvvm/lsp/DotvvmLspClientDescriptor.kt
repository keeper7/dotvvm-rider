package com.keeper7.dotvvm.lsp

import com.intellij.execution.configurations.GeneralCommandLine
import com.intellij.openapi.project.Project
import com.intellij.openapi.vfs.VirtualFile
import com.intellij.platform.lsp.api.Lsp4jClient
import com.intellij.platform.lsp.api.LspServerNotificationsHandler
import com.intellij.platform.lsp.api.ProjectWideLspClientDescriptor
import com.intellij.platform.lsp.api.customization.LspCustomization
import com.intellij.platform.lsp.api.customization.LspDocumentSymbolDisabled
import com.intellij.platform.lsp.api.customization.LspFoldingRangeDisabled
import com.intellij.platform.lsp.api.customization.LspFormattingDisabled
import com.intellij.platform.lsp.api.customization.LspOnTypeFormattingDisabled
import java.nio.file.Path

/**
 * Popisuje spojení s LSP serverem. V terminologii platformy je *klientem* IDE,
 * takže tato třída sídlí na straně klienta a server je externí proces.
 */
class DotvvmLspClientDescriptor(project: Project, private val serverDll: Path)
    : ProjectWideLspClientDescriptor(project, "DotVVM") {

    override fun isSupportedFile(file: VirtualFile): Boolean = isDotvvmFile(file)

    override fun createCommandLine(): GeneralCommandLine =
        GeneralCommandLine(ServerBinaryLocator.buildCommandLine(serverDll))
            .withWorkDirectory(project.basePath)
            .withCharset(Charsets.UTF_8)

    override fun createLsp4jClient(handler: LspServerNotificationsHandler): Lsp4jClient =
        DotvvmLsp4jClient(handler, project)

    /**
     * Formátování, skládání a strukturu souboru poskytuje nativní HTML podpora lépe než
     * LSP — server vidí soubor jen jako text. Vypnuté proto, aby se obě vrstvy nepraly
     * o stejnou věc. Completion, hover, go-to-definition a diagnostiky zůstávají na LSP,
     * protože stojí na znalosti projektu, kterou plugin nemá.
     */
    override val lspCustomization: LspCustomization = object : LspCustomization() {
        override val formattingCustomizer = LspFormattingDisabled
        override val onTypeFormattingCustomizer = LspOnTypeFormattingDisabled
        override val foldingRangeCustomizer = LspFoldingRangeDisabled
        override val documentSymbolCustomizer = LspDocumentSymbolDisabled
    }
}
