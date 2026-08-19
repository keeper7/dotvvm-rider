package com.keeper7.dotvvm.lang

import com.intellij.psi.xml.XmlFile
import com.intellij.testFramework.fixtures.BasePlatformTestCase

class DotvvmFileTypeTest : BasePlatformTestCase() {

    fun testDothtmlIsRecognizedAsDotvvmLanguage() {
        val file = myFixture.configureByText("Page.dothtml", "<html><body>text</body></html>")
        assertEquals(DotvvmLanguage.INSTANCE, file.language)
    }

    fun testDothtmlHasHtmlPsi() {
        val file = myFixture.configureByText("Page.dothtml", "<html><body>text</body></html>")
        assertTrue("očekáván XmlFile, byl ${file.javaClass.name}", file is XmlFile)
    }

    fun testDotcontrolAndDotmasterAreRecognized() {
        assertEquals(DotvvmLanguage.INSTANCE,
            myFixture.configureByText("C.dotcontrol", "<div/>").language)
        assertEquals(DotvvmLanguage.INSTANCE,
            myFixture.configureByText("M.dotmaster", "<div/>").language)
    }
}
