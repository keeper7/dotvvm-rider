package com.keeper7.dotvvm.lsp

import com.intellij.openapi.actionSystem.DataContext
import com.intellij.openapi.project.Project
import com.intellij.openapi.ui.popup.ListPopup
import com.intellij.openapi.util.Key
import com.intellij.openapi.vfs.VirtualFile
import com.intellij.openapi.wm.StatusBarWidget
import com.intellij.openapi.wm.StatusBarWidgetFactory
import com.intellij.openapi.wm.impl.status.EditorBasedStatusBarPopup

/** Stupeň konfigurace hlášený serverem; drží se na projektu, protože server je také projektový. */
val CONFIGURATION_TIER = Key.create<String>("dotvvm.configuration.tier")

const val CONFIGURATION_TIER_WIDGET_ID = "DotvvmConfigurationTier"

class DotvvmStatusBarWidget(project: Project)
    : EditorBasedStatusBarPopup(project, false) {

    override fun ID(): String = CONFIGURATION_TIER_WIDGET_ID

    override fun getWidgetState(file: VirtualFile?): WidgetState {
        if (file == null || !isDotvvmFile(file)) return WidgetState.HIDDEN

        val tier = project.getUserData(CONFIGURATION_TIER) ?: "základní"
        val tooltip = when (tier) {
            "plná"     -> "Kontrolky načteny ze sestavené assembly projektu"
            "config"   -> "Kontrolky načteny z konfigurace posledního spuštění aplikace"
            "základní" -> "Známy jsou jen standardní kontrolky. Sestav projekt pro plnou podporu."
            else       -> "Konfigurace DotVVM není k dispozici"
        }
        return WidgetState(tooltip, "DotVVM: $tier", true)
    }

    override fun createInstance(project: Project): StatusBarWidget =
        DotvvmStatusBarWidget(project)

    // Widget jen informuje; klikat není na co.
    override fun createPopup(context: DataContext): ListPopup? = null
}

class DotvvmStatusBarWidgetFactory : StatusBarWidgetFactory {
    override fun getId(): String = CONFIGURATION_TIER_WIDGET_ID
    override fun getDisplayName(): String = "DotVVM Configuration"
    override fun createWidget(project: Project): StatusBarWidget = DotvvmStatusBarWidget(project)
}
