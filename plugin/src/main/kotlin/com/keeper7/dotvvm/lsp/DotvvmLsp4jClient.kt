package com.keeper7.dotvvm.lsp

import com.intellij.openapi.project.Project
import com.intellij.openapi.wm.WindowManager
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

        // Widget musí stav přepočítat sám. `StatusBarWidgetsManager.updateWidget()` na to
        // nestačí — ten řeší jen dostupnost widgetu, takže už vykreslený widget by dál
        // ukazoval hodnotu z doby, kdy server ještě mlčel.
        val statusBar = WindowManager.getInstance().getStatusBar(project) ?: return
        (statusBar.getWidget(CONFIGURATION_TIER_WIDGET_ID) as? DotvvmStatusBarWidget)?.update()
    }
}
