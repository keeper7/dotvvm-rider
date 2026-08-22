package com.keeper7.dotvvm.lsp

import com.intellij.execution.configurations.GeneralCommandLine
import com.intellij.execution.configurations.PathEnvironmentVariableUtil
import com.intellij.openapi.diagnostic.Logger
import com.intellij.openapi.project.Project
import com.intellij.openapi.vfs.VirtualFile
import com.intellij.platform.lsp.api.Lsp4jClient
import com.intellij.platform.lsp.api.LspServerNotificationsHandler
import com.intellij.platform.lsp.api.ProjectWideLspClientDescriptor
import com.intellij.platform.lsp.api.customization.LspCompletionSupport
import com.intellij.platform.lsp.api.customization.LspCustomization
import com.intellij.platform.lsp.api.customization.LspDocumentSymbolDisabled
import com.intellij.platform.lsp.api.customization.LspFoldingRangeDisabled
import com.intellij.platform.lsp.api.customization.LspFormattingDisabled
import com.intellij.platform.lsp.api.customization.LspOnTypeFormattingDisabled
import java.nio.file.Files
import java.nio.file.Path

/**
 * Describes the connection to the LSP server. In the platform's terminology the IDE is the
 * *client*, so this class lives on the client side and the server is the external process.
 */
class DotvvmLspClientDescriptor(project: Project, private val serverDll: Path)
    : ProjectWideLspClientDescriptor(project, "DotVVM") {

    override fun isSupportedFile(file: VirtualFile): Boolean = isDotvvmFile(file)

    override fun createCommandLine(): GeneralCommandLine =
        GeneralCommandLine(ServerBinaryLocator.buildCommandLine(serverDll, findDotnet()))
            .withWorkDirectory(project.basePath)
            .withCharset(Charsets.UTF_8)

    /**
     * Locates the .NET runtime. An IDE started from the Dock or a desktop launcher inherits a
     * minimal PATH that holds no .NET installation, so a bare `dotnet` would fail there while
     * working perfectly when the same IDE is started from a terminal — the server would simply
     * never come up, with nothing to point at the cause.
     */
    private fun findDotnet(): String {
        PathEnvironmentVariableUtil.findInPath(DOTNET)?.let { return it.absolutePath }

        val searchPath = ServerBinaryLocator.dotnetSearchPath(
            dotnetRoot = System.getenv("DOTNET_ROOT"),
            userHome = System.getProperty("user.home"),
        )
        ServerBinaryLocator.findDotnet(searchPath, Files::isExecutable)?.let { return it.toString() }

        LOG.warn("No .NET runtime found in PATH or in $searchPath; falling back to '$DOTNET'")
        return DOTNET
    }

    override fun createLsp4jClient(handler: LspServerNotificationsHandler): Lsp4jClient =
        DotvvmLsp4jClient(handler, project)

    /**
     * Formatting, folding and document structure are served better by the native HTML support
     * than by LSP, since the server sees the file as text only. They are switched off so the two
     * layers do not fight over the same thing. Completion, hover, go-to-definition and
     * diagnostics stay with LSP, because they rest on project knowledge the plugin lacks.
     */
    override val lspCustomization: LspCustomization = object : LspCustomization() {
        override val formattingCustomizer = LspFormattingDisabled
        override val onTypeFormattingCustomizer = LspOnTypeFormattingDisabled
        override val foldingRangeCustomizer = LspFoldingRangeDisabled
        override val documentSymbolCustomizer = LspDocumentSymbolDisabled

        /**
         * The platform asks before honouring each trigger character the server declares, and
         * without this it did not act on our space — the property list opened only on the first
         * letter or on Ctrl+Space. Inside a tag a space is exactly where the next attribute
         * begins, so it is worth opting in.
         */
        override val completionCustomizer = object : LspCompletionSupport() {
            override fun isTriggerCharacterRespected(char: Char): Boolean = true
        }
    }

    private companion object {
        const val DOTNET = "dotnet"
        val LOG = Logger.getInstance(DotvvmLspClientDescriptor::class.java)
    }
}
