package com.keeper7.dotvvm.lsp

import com.intellij.ide.plugins.PluginManagerCore
import com.intellij.openapi.application.ApplicationManager
import com.intellij.openapi.diagnostic.logger
import com.intellij.openapi.extensions.PluginId
import com.intellij.openapi.project.Project
import com.intellij.openapi.vfs.VirtualFile
import com.intellij.platform.lsp.api.LspIntegrationProvider

private val LOG = logger<DotvvmLspIntegrationProvider>()

class DotvvmLspIntegrationProvider : LspIntegrationProvider {

    override fun fileOpened(
        project: Project,
        file: VirtualFile,
        serverStarter: LspIntegrationProvider.LspClientStarter
    ) {
        if (!isDotvvmFile(file)) return

        // A test that merely opens a .dothtml file has no use for the server, and nothing
        // stops it afterwards: the fixture tears the project down without closing the client,
        // so every run left a dotnet process behind. Forty-eight of them were found alive at
        // once, the oldest over a day old.
        if (ApplicationManager.getApplication().isUnitTestMode) return

        val serverDll = locateServer()
        if (serverDll == null) {
            LOG.warn("The LSP server was not found in the plugin distribution; advanced features stay off")
            return
        }

        serverStarter.ensureClientStarted(DotvvmLspClientDescriptor(project, serverDll))
    }

    private fun locateServer() =
        PluginManagerCore.getPlugin(PluginId.getId("com.keeper7.dotvvm"))
            ?.pluginPath
            ?.let { ServerBinaryLocator.findServerDll(it) }
}
