package com.keeper7.dotvvm.lsp

import com.intellij.openapi.application.ApplicationManager
import com.intellij.openapi.application.PluginPathManager
import com.intellij.openapi.diagnostic.logger
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

    /**
     * The bundled server, asked of the platform rather than of the plugin registry.
     *
     * `PluginManagerCore.getPlugin(PluginId)` did this until Marketplace's verifier reported it
     * as **internal API** on 0.4.0. `PluginPathManager` answers the same question in public API
     * and asks it better: the plugin is identified by a class of its own, so the id no longer
     * has to be repeated as a string that nothing checks against `plugin.xml`.
     *
     * It resolves the path without looking at the disk, which is why the file still has to be
     * confirmed - a distribution built without the server would otherwise reach `dotnet`.
     */
    private fun locateServer() =
        PluginPathManager.getPluginDistPath(javaClass, ServerBinaryLocator.SERVER_PATH)
            ?.let(ServerBinaryLocator::existing)
}
