package com.keeper7.dotvvm.lang

import com.intellij.openapi.fileTypes.LanguageFileType
import com.keeper7.dotvvm.ide.DotvvmIcons
import javax.swing.Icon

class DotHtmlFileType private constructor() : LanguageFileType(DotvvmLanguage.INSTANCE) {
    override fun getName(): String = "DotVVM Page"
    // Bez přepsání by se zdědilo jméno jazyka a všechny tři typy by se hlásily
    // jako "DotVVM" — platforma to hlásí jako varování při startu.
    override fun getDisplayName(): String = "DotVVM Page"
    override fun getDescription(): String = "DotVVM page"
    override fun getDefaultExtension(): String = "dothtml"
    override fun getIcon(): Icon = DotvvmIcons.DotHtml

    companion object { @JvmField val INSTANCE = DotHtmlFileType() }
}

class DotControlFileType private constructor() : LanguageFileType(DotvvmLanguage.INSTANCE) {
    override fun getName(): String = "DotVVM User Control"
    // Bez přepsání by se zdědilo jméno jazyka a všechny tři typy by se hlásily
    // jako "DotVVM" — platforma to hlásí jako varování při startu.
    override fun getDisplayName(): String = "DotVVM User Control"
    override fun getDescription(): String = "DotVVM user control"
    override fun getDefaultExtension(): String = "dotcontrol"
    override fun getIcon(): Icon = DotvvmIcons.DotControl

    companion object { @JvmField val INSTANCE = DotControlFileType() }
}

class DotMasterFileType private constructor() : LanguageFileType(DotvvmLanguage.INSTANCE) {
    override fun getName(): String = "DotVVM Master Page"
    // Bez přepsání by se zdědilo jméno jazyka a všechny tři typy by se hlásily
    // jako "DotVVM" — platforma to hlásí jako varování při startu.
    override fun getDisplayName(): String = "DotVVM Master Page"
    override fun getDescription(): String = "DotVVM master page"
    override fun getDefaultExtension(): String = "dotmaster"
    override fun getIcon(): Icon = DotvvmIcons.DotMaster

    companion object { @JvmField val INSTANCE = DotMasterFileType() }
}
