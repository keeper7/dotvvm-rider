package com.keeper7.dotvvm.ide

import com.intellij.codeInspection.htmlInspections.HtmlUnknownAttributeInspection
import com.intellij.lang.annotation.HighlightSeverity
import com.intellij.testFramework.fixtures.BasePlatformTestCase

class UnknownAttributeTest : BasePlatformTestCase() {

    private fun warnings(text: String): List<String> {
        myFixture.enableInspections(HtmlUnknownAttributeInspection())
        myFixture.configureByText("A.dotcontrol", text)
        return myFixture.doHighlighting()
            .filter { it.severity.myVal >= HighlightSeverity.WEAK_WARNING.myVal }
            .mapNotNull { it.description }
    }

    fun testDotvvmAttributesOnHtmlElementsAreAccepted() {
        val found = warnings(
            "<div>\n" +
            "  <label Class-required=\"{value: _c.Req}\">x</label>\n" +
            "  <span Visible=\"{value: _c.Show}\" Style-color=\"red\">y</span>\n" +
            "  <input Validator.Value=\"{value: _c.V}\" Validation.Enabled=\"false\" />\n" +
            "  <p DataContext=\"{value: _c.Item}\" IncludeInPage=\"true\">z</p>\n" +
            "</div>"
        )
        assertEmpty("Reported: " + found.joinToString(), found)
    }

    fun testAttributesOfPrefixedControlsAreAccepted() {
        // On a control every attribute is one of its properties; the platform cannot know them
        val found = warnings("<cc:MyControl Caption=\"Street\" Value=\"{value: _c.Street}\" />")
        assertEmpty("Reported: " + found.joinToString(), found)
    }

    fun testTypoInPlainHtmlAttributeIsStillReported() {
        // The suppressor must be narrow: a typo in an ordinary HTML attribute must still be reported
        val found = warnings("<div clas=\"x\">y</div>")
        assertNotEmpty(found)
    }
}
