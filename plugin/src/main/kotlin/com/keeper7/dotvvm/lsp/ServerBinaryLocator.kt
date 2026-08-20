package com.keeper7.dotvvm.lsp

import java.nio.file.Files
import java.nio.file.Path

/**
 * Najde LSP server přibalený v distribuci pluginu a sestaví příkaz pro jeho spuštění.
 *
 * Bez závislosti na IntelliJ API, aby šel otestovat obyčejným JUnit testem.
 */
object ServerBinaryLocator {

    private const val SERVER_DIR = "server"
    private const val SERVER_DLL = "DotVVM.LanguageServer.dll"

    fun findServerDll(pluginRoot: Path): Path? {
        val candidate = pluginRoot.resolve(SERVER_DIR).resolve(SERVER_DLL)
        return if (Files.isRegularFile(candidate)) candidate else null
    }

    /**
     * Server je publikovaný jako framework-dependent, takže se spouští přes `dotnet`.
     * Rider vlastní .NET runtime obsahuje.
     */
    fun buildCommandLine(dll: Path): List<String> = listOf("dotnet", dll.toString())
}
