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

        assertNotNull("Navigation found no target", targets)
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
        // Caret on the directive name, not on the path: there is nowhere to jump
        val file = myFixture.configureByText(
            "Page.dothtml", "@masterPage Views/Site.dotmaster\n<html></html>")
        val offset = 3

        val targets = MasterPageNavigationHandler()
            .getGotoDeclarationTargets(file.findElementAt(offset), offset, myFixture.editor)

        assertTrue(targets == null || targets.isEmpty())
    }
}
