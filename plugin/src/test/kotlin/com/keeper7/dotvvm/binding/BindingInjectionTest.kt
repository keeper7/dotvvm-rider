package com.keeper7.dotvvm.binding

import com.intellij.lang.injection.InjectedLanguageManager
import com.intellij.psi.util.PsiTreeUtil
import com.intellij.psi.xml.XmlAttributeValue
import com.intellij.testFramework.fixtures.BasePlatformTestCase

class BindingInjectionTest : BasePlatformTestCase() {

    private fun injectedLanguagesIn(text: String): List<String> {
        val file = myFixture.configureByText("Page.dothtml", text)
        val manager = InjectedLanguageManager.getInstance(project)
        val languages = mutableListOf<String>()
        PsiTreeUtil.processElements(file) { element ->
            manager.getInjectedPsiFiles(element)?.forEach { pair ->
                languages.add(pair.first.language.id)
            }
            true
        }
        return languages
    }

    fun testInjectsIntoAttributeValue() {
        val langs = injectedLanguagesIn("""<dot:TextBox Text="{value: Name}"/>""")
        assertTrue("expected a DotVVMBinding injection, found: $langs",
                   langs.contains("DotVVMBinding"))
    }

    fun testDoesNotInjectIntoPlainAttribute() {
        val langs = injectedLanguagesIn("""<div class="plain-value"/>""")
        assertFalse("an ordinary attribute must not be injected into: $langs",
                    langs.contains("DotVVMBinding"))
    }

    fun testInjectsIntoTextContent() {
        val langs = injectedLanguagesIn("""<span>{{value: Name}}</span>""")
        assertTrue("expected an injection into text, found: $langs",
                   langs.contains("DotVVMBinding"))
    }

    fun testAttributeValueWithNestedBracesIsInjectedAsOnePlace() {
        val file = myFixture.configureByText("Page.dothtml",
            """<dot:Repeater DataSource="{value: Items.Select(x => new { A = x.Id })}"/>""")
        val attr = PsiTreeUtil.findChildOfType(file, XmlAttributeValue::class.java)
        assertNotNull(attr)
        val manager = InjectedLanguageManager.getInstance(project)
        val injected = manager.getInjectedPsiFiles(attr!!)
        assertNotNull("expected an injection", injected)
        assertEquals("expected exactly one injected place", 1, injected!!.size)
    }
}
