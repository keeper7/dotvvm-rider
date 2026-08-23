package com.keeper7.dotvvm.lsp

import com.intellij.openapi.vfs.VirtualFile
import com.intellij.platform.lsp.api.LspIntegrationProvider
import com.intellij.platform.lsp.api.LspClientDescriptor
import com.intellij.testFramework.fixtures.BasePlatformTestCase

class LspNotStartedInTestsTest : BasePlatformTestCase() {

    private class RecordingStarter : LspIntegrationProvider.LspClientStarter {
        var started = 0
        override fun ensureClientStarted(descriptor: LspClientDescriptor) {
            started++
        }
    }

    /**
     * Every test that opened a .dothtml file used to leave a dotnet process behind: the fixture
     * tears the project down without closing the LSP client, and nothing else stops the server.
     * Forty-eight were found alive at once, the oldest over a day old.
     */
    fun testTheServerIsNotStartedFromATest() {
        val file = myFixture.configureByText("A.dothtml", "<html></html>").virtualFile
        val starter = RecordingStarter()

        DotvvmLspIntegrationProvider().fileOpened(project, file, starter)

        assertEquals("The LSP server must not be started from a test", 0, starter.started)
    }

    fun testAFileOfAnotherTypeIsIgnoredToo() {
        val file: VirtualFile = myFixture.configureByText("A.html", "<html></html>").virtualFile
        val starter = RecordingStarter()

        DotvvmLspIntegrationProvider().fileOpened(project, file, starter)

        assertEquals(0, starter.started)
    }
}
