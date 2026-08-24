package com.keeper7.dotvvm.binding

import com.intellij.lang.injection.InjectedLanguageManager
import com.intellij.psi.util.PsiUtilCore
import com.intellij.testFramework.fixtures.BasePlatformTestCase

/**
 * Which language the caret is in decides which completion contributors the platform runs, so it
 * decides where the offer has to be registered. A binding that is **finished** is injected and a
 * binding being typed is not, and completion happens in both.
 */
class BindingInjectedContextTest : BasePlatformTestCase() {

    /** The language the completion machinery looks its contributors up by. */
    private fun languageAtCaret(): String {
        val manager = InjectedLanguageManager.getInstance(project)
        val injected = manager.findInjectedElementAt(myFixture.file, myFixture.caretOffset)
        val file = injected?.containingFile ?: myFixture.file
        val offset = if (injected == null) myFixture.caretOffset
                     else myFixture.caretOffset - manager.injectedToHost(file, 0)
        return PsiUtilCore.getLanguageAtOffset(file, offset.coerceAtLeast(0)).id
    }

    fun testAFinishedBindingIsInjected() {
        myFixture.configureByText("A.dothtml", "<span>{{resource: <caret>}}</span>")

        assertEquals("DotVVMBinding", languageAtCaret())
    }

    fun testTheOneBeingTypedIsStillTheFilesOwnLanguage() {
        // And DotVVM extends HTML, which extends XML, so a contributor registered for XML is
        // found here through the base language chain - which is how the directive one works
        myFixture.configureByText("B.dothtml", "<span>{{re<caret></span>")

        assertEquals("DotVVM", languageAtCaret())
    }
}
