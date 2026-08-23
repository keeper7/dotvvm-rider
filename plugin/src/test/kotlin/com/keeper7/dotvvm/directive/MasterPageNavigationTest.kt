package com.keeper7.dotvvm.directive

import com.intellij.codeInsight.navigation.actions.GotoDeclarationAction
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

    fun testTheWholePlatformPathFindsIt() {
        // The tests above call the handler directly and so say nothing about whether the
        // platform ever asks it. This one goes the way Cmd+click goes.
        myFixture.addFileToProject("App.csproj", "<Project />")
        myFixture.addFileToProject("Views/Site.dotmaster", "<html></html>")
        myFixture.configureByText(
            "Page.dothtml", "@masterPage Views/Si<caret>te.dotmaster\n<html></html>")

        val target = GotoDeclarationAction.findTargetElement(
            project, myFixture.editor, myFixture.editor.caretModel.offset)

        assertEquals("Site.dotmaster", (target as? PsiFile)?.name)
    }

    fun testThePathIsRelativeToTheCsprojNotTheContentRoot() {
        // What the user hit: with the whole repository open, the content root sat above the
        // web app and `Views/Site.dotmaster` resolved against it, where nothing of the sort is.
        myFixture.addFileToProject("app/App.csproj", "<Project />")
        myFixture.addFileToProject("app/Views/Site.dotmaster", "<html></html>")
        myFixture.addFileToProject("Views/Site.dotmaster", "<html>the wrong one</html>")
        val file = myFixture.addFileToProject(
            "app/Pages/Page.dothtml", "@masterPage Views/Site.dotmaster\n<html></html>")

        val offset = file.text.indexOf("Views/Site") + 3
        val targets = MasterPageNavigationHandler()
            .getGotoDeclarationTargets(file.findElementAt(offset), offset, myFixture.editor)

        val found = targets?.singleOrNull() as? PsiFile
        assertNotNull("nothing found", found)
        assertEquals("the one beside the .csproj is the right one",
                     "the wrong one" !in found!!.text, true)
    }

    fun testFallsBackToTheContentRootWithoutACsproj() {
        // A bare folder of views has no project file to measure from; the content root is then
        // the best guess left, and it is what this used to do always
        myFixture.addFileToProject("Views/Site.dotmaster", "<html></html>")
        val file = myFixture.configureByText(
            "Page.dothtml", "@masterPage Views/Site.dotmaster\n<html></html>")

        val offset = file.text.indexOf("Views/Site") + 3
        val targets = MasterPageNavigationHandler()
            .getGotoDeclarationTargets(file.findElementAt(offset), offset, myFixture.editor)

        assertEquals("Site.dotmaster", (targets?.singleOrNull() as? PsiFile)?.name)
    }

    fun testJumpsFromViewModelToItsSource() {
        // The server answers textDocument/definition correctly, but the platform never asks it
        // here: the PSI holds a directive as bare XML_DATA_CHARACTERS under the document, and
        // on such a position no LSP link is even offered
        myFixture.addFileToProject(
            "ViewModels/HomeViewModel.cs", "namespace App.ViewModels;\npublic class HomeViewModel {}")
        myFixture.configureByText(
            "Page.dothtml", "@viewModel App.ViewModels.Home<caret>ViewModel\n<html></html>")

        val target = GotoDeclarationAction.findTargetElement(
            project, myFixture.editor, myFixture.editor.caretModel.offset)

        assertEquals("HomeViewModel.cs", (target as? PsiFile)?.name)
    }

    fun testTheAssemblyHalfIsNotPartOfTheName() {
        myFixture.addFileToProject(
            "ViewModels/HomeViewModel.cs", "namespace App.ViewModels;\npublic class HomeViewModel {}")
        val file = myFixture.configureByText(
            "Page.dothtml", "@viewModel App.ViewModels.HomeViewModel, App\n<html></html>")

        val offset = file.text.indexOf("HomeViewModel") + 3
        val targets = MasterPageNavigationHandler()
            .getGotoDeclarationTargets(file.findElementAt(offset), offset, myFixture.editor)

        assertEquals("HomeViewModel.cs", (targets?.singleOrNull() as? PsiFile)?.name)
    }

    fun testJumpsFromBaseTypeToo() {
        myFixture.addFileToProject(
            "Controls/Card.cs", "namespace App.Controls;\npublic class Card {}")
        val file = myFixture.configureByText(
            "C.dotcontrol", "@viewModel A\n@baseType App.Controls.Card\n<html></html>")

        val offset = file.text.indexOf("App.Controls.Card") + 14
        val targets = MasterPageNavigationHandler()
            .getGotoDeclarationTargets(file.findElementAt(offset), offset, myFixture.editor)

        assertEquals("Card.cs", (targets?.singleOrNull() as? PsiFile)?.name)
    }

    fun testNoTargetForATypeWithNoSource() {
        val file = myFixture.configureByText(
            "Page.dothtml", "@viewModel App.ViewModels.Nowhere\n<html></html>")

        val offset = file.text.indexOf("Nowhere") + 2
        val targets = MasterPageNavigationHandler()
            .getGotoDeclarationTargets(file.findElementAt(offset), offset, myFixture.editor)

        assertTrue(targets == null || targets.isEmpty())
    }
}
