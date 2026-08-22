package com.keeper7.dotvvm.lsp

import java.nio.file.Files
import java.nio.file.Path

/**
 * Finds the LSP server bundled in the plugin distribution and builds the command to start it.
 *
 * Free of IntelliJ API so it can be tested with a plain JUnit test.
 */
object ServerBinaryLocator {

    private const val SERVER_DIR = "server"
    private const val SERVER_DLL = "DotVVM.LanguageServer.dll"

    fun findServerDll(pluginRoot: Path): Path? {
        val candidate = pluginRoot.resolve(SERVER_DIR).resolve(SERVER_DLL)
        return if (Files.isRegularFile(candidate)) candidate else null
    }

    /**
     * The server is published framework-dependent, so it is started through `dotnet`.
     * Rider ships a .NET runtime of its own.
     */
    fun buildCommandLine(dll: Path): List<String> = listOf("dotnet", dll.toString())
}
