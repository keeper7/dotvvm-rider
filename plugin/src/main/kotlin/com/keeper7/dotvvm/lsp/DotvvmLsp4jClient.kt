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

/** One registration as it arrives over the wire. Every field can be absent, hence the nulls. */
class ControlRegistrationParams {
    @JvmField var prefix: String? = null
    @JvmField var tagName: String? = null
    @JvmField var src: String? = null
    @JvmField var namespace: String? = null
    @JvmField var assembly: String? = null
}

/** Shape of `dotvvm/controlRegistrations`. */
class ControlRegistrationsParams {
    @JvmField var registrations: List<ControlRegistrationParams>? = null
}

/**
 * Extends the standard client with the server's own notifications. Without it the status bar
 * would have nowhere to read the configuration source from, and navigation out of a tag nothing
 * to resolve against — the LSP protocol has nothing standard for either.
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

    /**
     * The control registrations, which only the server can know. They arrive alongside the tier
     * because they change together: a new tier is a new registry.
     */
    @JsonNotification("dotvvm/controlRegistrations")
    fun controlRegistrations(params: ControlRegistrationsParams) {
        val registrations = params.registrations.orEmpty().mapNotNull { entry ->
            val prefix = entry.prefix ?: return@mapNotNull null
            ControlRegistration(prefix, entry.tagName, entry.src, entry.namespace, entry.assembly)
        }
        ControlRegistrations.of(project).update(registrations)
    }
}
