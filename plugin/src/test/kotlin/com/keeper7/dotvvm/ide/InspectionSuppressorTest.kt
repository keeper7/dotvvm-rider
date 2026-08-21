package com.keeper7.dotvvm.ide

import com.intellij.testFramework.fixtures.BasePlatformTestCase

class InspectionSuppressorTest : BasePlatformTestCase() {

    fun testUnboundPrefixIsSuppressedInDotvvmFile() {
        val file = myFixture.configureByText(
            "Sample.dothtml",
            "<html><body><dot:Button Text=\"x\" /></body></html>"
        )
        val tag = file.findElementAt(file.text.indexOf("dot:Button"))!!

        assertTrue(
            "Inspekce nevázaného prefixu musí být pro DotVVM potlačena",
            DotvvmInspectionSuppressor().isSuppressedFor(tag, "XmlUnboundNsPrefix")
        )
    }

    fun testOtherInspectionsAreNotSuppressed() {
        // Suppressor musí být úzký — nesmí umlčet všechno
        val file = myFixture.configureByText(
            "Sample.dothtml",
            "<html><body><dot:Button Text=\"x\" /></body></html>"
        )
        val tag = file.findElementAt(file.text.indexOf("dot:Button"))!!

        assertFalse(
            DotvvmInspectionSuppressor().isSuppressedFor(tag, "HtmlUnknownTag")
        )
    }
}
