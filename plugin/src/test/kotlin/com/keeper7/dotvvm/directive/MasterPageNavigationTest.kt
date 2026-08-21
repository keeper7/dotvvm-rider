package com.keeper7.dotvvm.directive

import com.intellij.psi.PsiFile
import com.intellij.testFramework.fixtures.BasePlatformTestCase

class MasterPageNavigationTest : BasePlatformTestCase() {

    fun testJumpsToMasterPageFile() {
        myFixture.addFileToProject("Views/Site.dotmaster", "<html></html>")
        val file = myFixture.configureByText(
            "Page.dothtml", "@masterPage Views/Site.dotmaster\n<html></html>")

        val offset = file.text.indexOf("Views/Site.dotmaster") + 3
        val targets = MasterPageNavigationHandler()
            .getGotoDeclarationTargets(file.findElementAt(offset), offset, myFixture.editor)

        assertNotNull("Navigace nenašla cíl", targets)
        assertEquals("Site.dotmaster", (targets!!.single() as PsiFile).name)
    }

    fun testNoTargetForMissingFile() {
        val file = myFixture.configureByText(
            "Page.dothtml", "@masterPage Views/Nowhere.dotmaster\n<html></html>")
        val offset = file.text.indexOf("Views/Nowhere") + 3

        val targets = MasterPageNavigationHandler()
            .getGotoDeclarationTargets(file.findElementAt(offset), offset, myFixture.editor)

        assertTrue(targets == null || targets.isEmpty())
    }

    fun testNoTargetOutsideDirectiveValue() {
        // Kurzor na názvu direktivy, ne na cestě — skákat není kam
        val file = myFixture.configureByText(
            "Page.dothtml", "@masterPage Views/Site.dotmaster\n<html></html>")
        val offset = 3

        val targets = MasterPageNavigationHandler()
            .getGotoDeclarationTargets(file.findElementAt(offset), offset, myFixture.editor)

        assertTrue(targets == null || targets.isEmpty())
    }
}
