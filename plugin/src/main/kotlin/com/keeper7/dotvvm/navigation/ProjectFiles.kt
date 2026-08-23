package com.keeper7.dotvvm.navigation

import com.intellij.openapi.project.Project
import com.intellij.openapi.roots.ProjectRootManager
import com.intellij.openapi.vfs.VirtualFile
import com.intellij.psi.PsiFile
import com.intellij.psi.PsiManager
import com.intellij.psi.search.FilenameIndex
import com.intellij.psi.search.GlobalSearchScope

/**
 * Finding the files a DotVVM view points at. Shared by everything that navigates out of a view,
 * because a directive's path and a markup control's `Src` are read by exactly the same rule.
 */
object ProjectFiles {

    /**
     * The nearest directory upwards that holds a .csproj — the DotVVM project's root, which is
     * what a path in a view is relative to, and not a content root of the IDE. The two differ
     * whenever the opened project is larger than the web app.
     */
    fun projectRootOf(file: VirtualFile?): VirtualFile? {
        var dir = file?.parent
        while (dir != null) {
            if (dir.children?.any { it.extension == "csproj" } == true) return dir
            dir = dir.parent
        }
        return null
    }

    /** Resolves a project-relative path, falling back to the content roots when there is no .csproj. */
    fun resolve(file: PsiFile, path: String): VirtualFile? {
        projectRootOf(file.viewProvider.virtualFile)?.findFileByRelativePath(path)?.let { return it }

        // No .csproj anywhere above — a bare folder of views, say. The content roots are then
        // the best guess left, and walking all of them is cheap.
        return ProjectRootManager.getInstance(file.project).contentRoots
            .firstNotNullOfOrNull { it.findFileByRelativePath(path) }
    }

    /**
     * The file declaring the type, searched by the last segment of its name — a class is
     * routinely named differently from the file that uses it, and the file is named after the
     * class. Anything after a comma is the assembly and not part of the name.
     */
    fun findTypeSource(project: Project, typeName: String): PsiFile? {
        val bare = typeName.substringBefore(',').trim().substringBefore('<')
        val shortName = bare.substringAfterLast('.')
        if (shortName.isEmpty()) return null

        val candidates = FilenameIndex.getVirtualFilesByName(
            "$shortName.cs", GlobalSearchScope.projectScope(project))
        val manager = PsiManager.getInstance(project)

        // The file named after the class is the usual case; when several match, the one that
        // really declares it wins
        val declaring = candidates.firstOrNull { candidate ->
            manager.findFile(candidate)?.text?.contains("class $shortName") == true
        }
        return manager.findFile(declaring ?: candidates.firstOrNull() ?: return null)
    }

    /**
     * The project building that assembly. An assembly has no source of its own; the .csproj is
     * the nearest thing there is, and its name matches the assembly unless the project renames
     * it with <AssemblyName>, which nothing in reach does.
     */
    fun findAssemblyProject(project: Project, assembly: String): PsiFile? {
        val name = assembly.substringBefore(',').trim()
        if (name.isEmpty()) return null

        val candidates = FilenameIndex.getVirtualFilesByName(
            "$name.csproj", GlobalSearchScope.projectScope(project))
        return PsiManager.getInstance(project).findFile(candidates.firstOrNull() ?: return null)
    }
}
