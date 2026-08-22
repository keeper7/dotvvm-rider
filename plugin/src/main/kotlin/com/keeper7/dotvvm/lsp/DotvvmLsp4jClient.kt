package com.keeper7.dotvvm.lsp

import com.intellij.openapi.project.Project
import com.intellij.openapi.wm.WindowManager
import com.intellij.platform.lsp.api.Lsp4jClient
import com.intellij.platform.lsp.api.LspServerNotificationsHandler
import org.eclipse.lsp4j.jsonrpc.services.JsonNotification

/** Shape of `dotvvm/configurationTier`; lsp4j deserialises the notification parameters into it. */
class ConfigurationTierParams {
    @JvmField var tier: String? = null
}

/**
 * Extends the standard client with the server's single custom notification. Without it the
 * status bar would have nowhere to read the configuration source from — the LSP protocol has
 * nothing standard for this.
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

        // The widget has to recompute its state itself. `StatusBarWidgetsManager.updateWidget()`
        // is not enough: it only re-evaluates whether the widget is available, so an already
        // painted widget would keep showing the value from before the server answered.
        val statusBar = WindowManager.getInstance().getStatusBar(project) ?: return
        (statusBar.getWidget(CONFIGURATION_TIER_WIDGET_ID) as? DotvvmStatusBarWidget)?.update()
    }
}
