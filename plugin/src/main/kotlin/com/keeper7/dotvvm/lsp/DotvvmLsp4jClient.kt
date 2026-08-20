package com.keeper7.dotvvm.lsp

import com.intellij.openapi.components.service
import com.intellij.openapi.project.Project
import com.intellij.openapi.wm.impl.status.widget.StatusBarWidgetsManager
import com.intellij.platform.lsp.api.Lsp4jClient
import com.intellij.platform.lsp.api.LspServerNotificationsHandler
import org.eclipse.lsp4j.jsonrpc.services.JsonNotification

/** Tvar `dotvvm/configurationTier`; lsp4j do něj deserializuje parametry notifikace. */
class ConfigurationTierParams {
    @JvmField var tier: String? = null
}

/**
 * Rozšiřuje standardního klienta o jedinou vlastní notifikaci serveru. Bez ní by
 * status bar neměl odkud stupeň konfigurace vzít — LSP protokol pro tuto informaci
 * nic standardního nemá.
 */
class DotvvmLsp4jClient(
    handler: LspServerNotificationsHandler,
    private val project: Project
) : Lsp4jClient(handler) {

    @JsonNotification("dotvvm/configurationTier")
    fun configurationTier(params: ConfigurationTierParams) {
        val tier = params.tier ?: return
        if (project.getUserData(CONFIGURATION_TIER) == tier) return

        project.putUserData(CONFIGURATION_TIER, tier)
        // Server posílá stupeň při každé změně dokumentu, ale překreslujeme jen při
        // skutečné změně — jinak by widget blikal při každém stisku klávesy.
        project.service<StatusBarWidgetsManager>()
            .updateWidget(DotvvmStatusBarWidgetFactory::class.java)
    }
}
