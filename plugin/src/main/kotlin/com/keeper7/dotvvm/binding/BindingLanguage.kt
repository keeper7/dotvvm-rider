package com.keeper7.dotvvm.binding

import com.intellij.lang.Language

class BindingLanguage private constructor() : Language("DotVVMBinding") {
    companion object { @JvmField val INSTANCE = BindingLanguage() }
}
