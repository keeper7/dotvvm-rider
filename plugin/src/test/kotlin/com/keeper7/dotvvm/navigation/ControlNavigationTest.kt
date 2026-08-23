package com.keeper7.dotvvm.navigation

import com.intellij.codeInsight.navigation.actions.GotoDeclarationAction
import com.intellij.psi.PsiFile
import com.intellij.testFramework.fixtures.BasePlatformTestCase
import com.keeper7.dotvvm.lsp.ControlRegistration
import com.keeper7.dotvvm.lsp.ControlRegistrations

class ControlNavigationTest : BasePlatformTestCase() {

    private fun register(vararg registrations: ControlRegistration) =
        ControlRegistrations.of(project).update(registrations.toList())

    private fun markupControl(prefix: String, tagName: String, src: String) =
        ControlRegistration(prefix, tagName, src, null, null)

    private fun namespace(prefix: String, namespace: String, assembly: String? = null) =
        ControlRegistration(prefix, null, null, namespace, assembly)

    fun testJumpsToTheMarkupControlFile() {
        myFixture.addFileToProject("App.csproj", "<Project />")
        myFixture.addFileToProject("Controls/MyControl.dotcontrol", "@baseType X\n<div></div>")
        register(markupControl("cc", "MyControl", "Controls/MyControl.dotcontrol"))

        myFixture.configureByText(
            "Page.dothtml", "<html><body><cc:MyCon<caret>trol /></body></html>")

        val target = GotoDeclarationAction.findTargetElement(
            project, myFixture.editor, myFixture.editor.caretModel.offset)

        assertEquals("MyControl.dotcontrol", (target as? PsiFile)?.name)
    }

    /**
     * A control registered by namespace has no file of its own; the class declaring it is the
     * nearest thing to a definition there is.
     */
    fun testJumpsToTheSourceOfAControlRegisteredByNamespace() {
        myFixture.addFileToProject("App.csproj", "<Project />")
        myFixture.addFileToProject(
            "Controls/Widget.cs", "namespace MyApp.Controls { public class Widget { } }")
        register(namespace("cc", "MyApp.Controls", "MyApp"))

        myFixture.configureByText("Page.dothtml", "<html><body><cc:Wid<caret>get /></body></html>")

        val target = GotoDeclarationAction.findTargetElement(
            project, myFixture.editor, myFixture.editor.caretModel.offset)

        assertEquals("Widget.cs", (target as? PsiFile)?.name)
    }

    /**
     * One prefix routinely stands for several namespaces — the project's own controls and a
     * library's. Taking the first registration would find the class only by luck.
     */
    fun testTriesEveryNamespaceThePrefixStandsFor() {
        myFixture.addFileToProject("App.csproj", "<Project />")
        myFixture.addFileToProject(
            "Controls/Widget.cs", "namespace MyApp.Second { public class Widget { } }")
        register(namespace("cc", "MyApp.First"), namespace("cc", "MyApp.Second"))

        myFixture.configureByText("Page.dothtml", "<html><body><cc:Wid<caret>get /></body></html>")

        val target = GotoDeclarationAction.findTargetElement(
            project, myFixture.editor, myFixture.editor.caretModel.offset)

        assertEquals("Widget.cs", (target as? PsiFile)?.name)
    }

    /** The closing tag names the same control, and a reader clicks it just as readily. */
    fun testJumpsFromTheClosingTagToo() {
        myFixture.addFileToProject("App.csproj", "<Project />")
        myFixture.addFileToProject("Controls/MyControl.dotcontrol", "<div></div>")
        register(markupControl("cc", "MyControl", "Controls/MyControl.dotcontrol"))

        myFixture.configureByText(
            "Page.dothtml", "<html><body><cc:MyControl></cc:MyCon<caret>trol></body></html>")

        val target = GotoDeclarationAction.findTargetElement(
            project, myFixture.editor, myFixture.editor.caretModel.offset)

        assertEquals("MyControl.dotcontrol", (target as? PsiFile)?.name)
    }

    fun testLeavesAPlainHtmlTagAlone() {
        myFixture.addFileToProject("App.csproj", "<Project />")
        register(namespace("dot", "DotVVM.Framework.Controls"))
        val file = myFixture.configureByText("Page.dothtml", "<html><body><div /></body></html>")

        val offset = file.text.indexOf("div") + 1
        val targets = ControlNavigationHandler()
            .getGotoDeclarationTargets(file.findElementAt(offset), offset, myFixture.editor)

        assertNull(targets)
    }

    /** With no registration for the prefix there is nothing to point at, and guessing would lie. */
    fun testSaysNothingWhenThePrefixIsUnknown() {
        myFixture.addFileToProject("App.csproj", "<Project />")
        register()
        val file = myFixture.configureByText(
            "Page.dothtml", "<html><body><cc:MyControl /></body></html>")

        val offset = file.text.indexOf("cc:MyControl") + 4
        val targets = ControlNavigationHandler()
            .getGotoDeclarationTargets(file.findElementAt(offset), offset, myFixture.editor)

        assertTrue(targets == null || targets.isEmpty())
    }

    /**
     * DotVVM registers a few of its own controls under embedded://, which names a resource
     * inside an assembly. Resolving it as a path would land nowhere.
     */
    fun testSkipsAnEmbeddedSource() {
        myFixture.addFileToProject("App.csproj", "<Project />")
        register(markupControl("dot", "Internal", "embedded://DotVVM.Framework/Internal.dotcontrol"))
        val file = myFixture.configureByText(
            "Page.dothtml", "<html><body><dot:Internal /></body></html>")

        val offset = file.text.indexOf("dot:Internal") + 5
        val targets = ControlNavigationHandler()
            .getGotoDeclarationTargets(file.findElementAt(offset), offset, myFixture.editor)

        assertTrue(targets == null || targets.isEmpty())
    }

    /**
     * The registration carries the name as the author wrote it, while an HTML tag reports its
     * own name lower-cased — matching on that would find nothing at all.
     */
    fun testMatchesTheNameAsWrittenRatherThanLowerCased() {
        myFixture.addFileToProject("App.csproj", "<Project />")
        myFixture.addFileToProject("Controls/MyControl.dotcontrol", "<div></div>")
        register(markupControl("cc", "MyControl", "Controls/MyControl.dotcontrol"))

        val file = myFixture.configureByText(
            "Page.dothtml", "<html><body><cc:MyControl /></body></html>")

        val offset = file.text.indexOf("cc:MyControl") + 4
        val targets = ControlNavigationHandler()
            .getGotoDeclarationTargets(file.findElementAt(offset), offset, myFixture.editor)

        assertNotNull("Navigation found no target", targets)
        assertEquals("MyControl.dotcontrol", (targets!!.single() as PsiFile).name)
    }
}
