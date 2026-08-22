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
        assertTrue("expected an XmlFile, got ${file.javaClass.name}", file is XmlFile)
    }

    /**
     * The platform warns when two file types share getDisplayName(); it is inherited from the
     * language, so without an override all three would return "DotVVM".
     */
    fun testFileTypesHaveDistinctDisplayNames() {
        val names = listOf(
            DotHtmlFileType.INSTANCE.displayName,
            DotControlFileType.INSTANCE.displayName,
            DotMasterFileType.INSTANCE.displayName,
        )
        assertEquals("display names must be unique, they were: $names", 3, names.toSet().size)
    }

    fun testDotcontrolAndDotmasterAreRecognized() {
        assertEquals(DotvvmLanguage.INSTANCE,
            myFixture.configureByText("C.dotcontrol", "<div/>").language)
        assertEquals(DotvvmLanguage.INSTANCE,
            myFixture.configureByText("M.dotmaster", "<div/>").language)
    }
}
