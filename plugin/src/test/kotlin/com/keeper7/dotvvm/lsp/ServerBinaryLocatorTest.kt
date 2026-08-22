package com.keeper7.dotvvm.lsp

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.junit.rules.TemporaryFolder
import java.nio.file.Path

class ServerBinaryLocatorTest {

    @get:Rule val temp = TemporaryFolder()

    @Test fun findsServerDllInExpectedLocation() {
        val root = temp.newFolder("plugin").toPath()
        val serverDir = root.resolve("server")
        serverDir.toFile().mkdirs()
        val dll = serverDir.resolve("DotVVM.LanguageServer.dll")
        dll.toFile().writeText("")

        assertEquals(dll, ServerBinaryLocator.findServerDll(root))
    }

    @Test fun returnsNullWhenServerMissing() {
        val root = temp.newFolder("empty").toPath()
        assertNull(ServerBinaryLocator.findServerDll(root))
    }

    @Test fun buildsDotnetCommandLine() {
        val dll = Path.of("/plugins/dotvvm/server/DotVVM.LanguageServer.dll")
        val cmd = ServerBinaryLocator.buildCommandLine(dll)
        assertEquals("dotnet", cmd[0])
        assertEquals(dll.toString(), cmd[1])
    }

    @Test fun buildsCommandLineWithResolvedRuntime() {
        val dll = Path.of("/plugins/dotvvm/server/DotVVM.LanguageServer.dll")
        val cmd = ServerBinaryLocator.buildCommandLine(dll, "/usr/local/share/dotnet/dotnet")
        assertEquals("/usr/local/share/dotnet/dotnet", cmd[0])
        assertEquals(dll.toString(), cmd[1])
    }

    @Test fun searchPathStartsWithDotnetRoot() {
        val searchPath = ServerBinaryLocator.dotnetSearchPath("/opt/dotnet", "/Users/someone")
        assertEquals(Path.of("/opt/dotnet"), searchPath.first())
    }

    @Test fun searchPathCoversTheUsualInstallLocations() {
        val searchPath = ServerBinaryLocator.dotnetSearchPath(null, "/Users/someone")

        // The macOS installer's location — the one a GUI-launched IDE cannot see
        assertTrue(searchPath.contains(Path.of("/usr/local/share/dotnet")))
        assertTrue(searchPath.contains(Path.of("/usr/share/dotnet")))
        assertTrue(searchPath.contains(Path.of("/opt/homebrew/bin")))
        assertTrue(searchPath.contains(Path.of("/Users/someone/.dotnet")))
    }

    @Test fun searchPathSkipsUnsetVariables() {
        val searchPath = ServerBinaryLocator.dotnetSearchPath(null, null)

        assertTrue(searchPath.none { it.toString().contains("null") })
        assertTrue(searchPath.isNotEmpty())
    }

    @Test fun findsFirstExecutableInSearchPath() {
        val searchPath = listOf(Path.of("/nowhere"), Path.of("/opt/dotnet"), Path.of("/usr/share/dotnet"))
        val present = setOf(Path.of("/opt/dotnet/dotnet"), Path.of("/usr/share/dotnet/dotnet"))

        assertEquals(Path.of("/opt/dotnet/dotnet"), ServerBinaryLocator.findDotnet(searchPath, present::contains))
    }

    @Test fun findsNoRuntimeWhenNoneIsExecutable() {
        val searchPath = listOf(Path.of("/nowhere"), Path.of("/elsewhere"))
        assertNull(ServerBinaryLocator.findDotnet(searchPath) { false })
    }
}
