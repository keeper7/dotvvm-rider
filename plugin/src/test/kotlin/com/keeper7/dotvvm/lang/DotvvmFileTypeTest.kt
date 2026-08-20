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

    /**
     * Platforma hlásí varování, když dva file typy sdílejí getDisplayName() —
     * dědí se z jazyka, takže bez přepsání vrátí všechny tři "DotVVM".
     */
    fun testFileTypesHaveDistinctDisplayNames() {
        val names = listOf(
            DotHtmlFileType.INSTANCE.displayName,
            DotControlFileType.INSTANCE.displayName,
            DotMasterFileType.INSTANCE.displayName,
        )
        assertEquals("zobrazovaná jména musí být unikátní, byla: $names", 3, names.toSet().size)
    }

    fun testDotcontrolAndDotmasterAreRecognized() {
        assertEquals(DotvvmLanguage.INSTANCE,
            myFixture.configureByText("C.dotcontrol", "<div/>").language)
        assertEquals(DotvvmLanguage.INSTANCE,
            myFixture.configureByText("M.dotmaster", "<div/>").language)
    }
}
