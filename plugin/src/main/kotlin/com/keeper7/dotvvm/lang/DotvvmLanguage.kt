package com.keeper7.dotvvm.lang

import com.intellij.lang.html.HTMLLanguage

/**
 * DotVVM is a superset of HTML: by extending [HTMLLanguage] the plugin gets all of the
 * CSS a JS podporu platformy zdarma.
 *
 * [HTMLLanguage.INSTANCE] is passed directly rather than through a `val` of our own in the
 * companion object, to avoid an initialisation order trap.
 */
class DotvvmLanguage private constructor() : HTMLLanguage(HTMLLanguage.INSTANCE, "DotVVM") {
    companion object {
        @JvmField
        val INSTANCE = DotvvmLanguage()
    }
}
