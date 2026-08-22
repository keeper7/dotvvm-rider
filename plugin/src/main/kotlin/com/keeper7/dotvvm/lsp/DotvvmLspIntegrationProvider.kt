package com.keeper7.dotvvm.lsp

import com.intellij.ide.plugins.PluginManagerCore
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
