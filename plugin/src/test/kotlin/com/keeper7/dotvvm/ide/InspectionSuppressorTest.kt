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
            "The unbound prefix inspection must be suppressed for DotVVM",
            DotvvmInspectionSuppressor().isSuppressedFor(tag, "XmlUnboundNsPrefix")
        )
    }

    fun testOtherInspectionsAreNotSuppressed() {
        // The suppressor must be narrow: it must not silence everything
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
