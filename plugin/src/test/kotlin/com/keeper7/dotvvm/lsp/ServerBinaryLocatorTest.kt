package com.keeper7.dotvvm.lsp

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
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
}
