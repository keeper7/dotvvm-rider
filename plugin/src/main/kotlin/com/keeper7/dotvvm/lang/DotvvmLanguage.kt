package com.keeper7.dotvvm.lang

import com.intellij.lang.html.HTMLLanguage

/**
 * DotVVM je nadmnožina HTML — děděním z [HTMLLanguage] získá plugin veškerou HTML,
 * CSS a JS podporu platformy zdarma.
 *
 * [HTMLLanguage.INSTANCE] se předává přímo, nikoli přes vlastní `val` v companion
 * objektu, aby se předešlo záludnosti s pořadím inicializace.
 */
class DotvvmLanguage private constructor() : HTMLLanguage(HTMLLanguage.INSTANCE, "DotVVM") {
    companion object {
        @JvmField
        val INSTANCE = DotvvmLanguage()
    }
}
