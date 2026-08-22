package com.keeper7.dotvvm.lsp

import java.nio.file.Files
import java.nio.file.Path

/**
 * Finds the LSP server bundled in the plugin distribution and the .NET runtime that starts it.
 *
 * Free of IntelliJ API so it can be tested with a plain JUnit test.
 */
object ServerBinaryLocator {

    private const val SERVER_DIR = "server"
    private const val SERVER_DLL = "DotVVM.LanguageServer.dll"
    private const val DOTNET = "dotnet"

    fun findServerDll(pluginRoot: Path): Path? {
        val candidate = pluginRoot.resolve(SERVER_DIR).resolve(SERVER_DLL)
        return if (Files.isRegularFile(candidate)) candidate else null
    }

    /**
     * Directories that hold a .NET runtime, in the order they should be searched.
     *
     * An IDE started from the Dock or a desktop launcher inherits a minimal PATH — on macOS
     * `/usr/bin:/bin:/usr/sbin:/sbin` — which contains none of these. Relying on PATH alone
     * therefore works when the IDE was started from a terminal and fails silently otherwise.
     */
    fun dotnetSearchPath(dotnetRoot: String?, userHome: String?): List<Path> = listOfNotNull(
        dotnetRoot,                 // an explicit choice wins over anything found by guessing
        "/usr/local/share/dotnet",  // macOS installer
        "/usr/share/dotnet",        // Linux packages
        "/usr/lib/dotnet",          // Linux, newer layout
        "/opt/homebrew/bin",        // Homebrew on Apple Silicon
        "/usr/local/bin",           // Homebrew on Intel, and a common symlink target
        userHome?.let { "$it/.dotnet" },  // dotnet-install.sh
    ).map(Path::of)

    fun findDotnet(searchPath: List<Path>, isExecutable: (Path) -> Boolean): Path? =
        searchPath.map { it.resolve(DOTNET) }.firstOrNull(isExecutable)

    /**
     * The server is published framework-dependent, so it is started through the runtime.
     * Falling back to the bare command keeps the old behaviour when nothing was found —
     * it may still work, and it fails with a message naming what is missing.
     */
    fun buildCommandLine(dll: Path, dotnet: String = DOTNET): List<String> =
        listOf(dotnet, dll.toString())
}
