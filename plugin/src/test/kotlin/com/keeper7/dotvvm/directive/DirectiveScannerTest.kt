package com.keeper7.dotvvm.directive

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class DirectiveScannerTest {

    @Test fun findsSingleDirective() {
        val text = "@viewModel App.MyViewModel, App\n<html></html>"
        val found = DirectiveScanner.scan(text)
        assertEquals(1, found.size)
        assertEquals("viewModel", found[0].name)
        assertEquals("App.MyViewModel, App", found[0].value)
        assertEquals(0, found[0].start)
        assertEquals("@viewModel App.MyViewModel, App".length, found[0].end)
    }

    @Test fun findsSeveralDirectives() {
        val text = "@viewModel App.Vm\n@masterPage Views/Site.dotmaster\n<html></html>"
        val found = DirectiveScanner.scan(text)
        assertEquals(listOf("viewModel", "masterPage"), found.map { it.name })
    }

    @Test fun skipsBlankLinesBetweenDirectives() {
        val text = "@viewModel App.Vm\n\n@import App.Controls\n<html></html>"
        assertEquals(2, DirectiveScanner.scan(text).size)
    }

    @Test fun stopsAtFirstTag() {
        // After the first tag there are no directives: @something in the body is plain text
        val text = "@viewModel App.Vm\n<html>\n@viewModel App.Other\n</html>"
        val found = DirectiveScanner.scan(text)
        assertEquals(1, found.size)
    }

    @Test fun stopsAtDoctype() {
        val text = "@viewModel App.Vm\n<!DOCTYPE html>\n<html></html>"
        assertEquals(1, DirectiveScanner.scan(text).size)
    }

    @Test fun ignoresUnknownDirectiveName() {
        // An unknown name is not a directive; the scanner must not swallow foreign text
        val text = "@nonsense whatever\n<html></html>"
        assertTrue(DirectiveScanner.scan(text).isEmpty())
    }

    @Test fun returnsEmptyListWhenFileStartsWithTag() {
        assertTrue(DirectiveScanner.scan("<html></html>").isEmpty())
    }

    @Test fun handlesDirectiveWithoutValue() {
        val text = "@noWrapperTag\n<html></html>"
        val found = DirectiveScanner.scan(text)
        assertEquals(1, found.size)
        assertEquals("", found[0].value)
    }

    @Test fun handlesCrLfLineEndings() {
        val text = "@viewModel App.Vm\r\n<html></html>"
        val found = DirectiveScanner.scan(text)
        assertEquals(1, found.size)
        assertEquals("App.Vm", found[0].value)
    }

    @Test fun keepsCommaInsideGenericArguments() {
        // The scanner does not split the value into type and assembly; the server does that
        // (FindAssemblySeparator in ViewModelDirective.cs). All that matters here is that
        // neither commas nor angle brackets end the directive early.
        val text = "@viewModel App.Vm<A, B>, App\n<html></html>"
        val found = DirectiveScanner.scan(text)
        assertEquals("App.Vm<A, B>, App", found[0].value)
    }

    @Test fun leadingWhitespaceIsAllowed() {
        val text = "   @viewModel App.Vm\n<html></html>"
        val found = DirectiveScanner.scan(text)
        assertEquals(1, found.size)
        assertEquals(3, found[0].start)
    }

    @Test fun findsDirectivesInFileStartingWithBom() {
        // Files saved by Visual Studio start with a BOM; without skipping it the scanner
        // would not even see the first directive
        val text = "\uFEFF@viewModel System.Object\n@noWrapperTag\n<div></div>"
        val found = DirectiveScanner.scan(text)
        assertEquals(listOf("viewModel", "noWrapperTag"), found.map { it.name })
        assertEquals(1, found[0].start)
    }

    @Test fun knownNamesContainViewModel() {
        assertTrue(DirectiveScanner.KNOWN_NAMES.contains("viewModel"))
    }

    @Test fun knowsExactlyTheDirectivesDotvvmDefines() {
        // The source of truth is ParserConstants in DotVVM.Framework 4.3.17, read by
        // reflection. The parser reports no error for an unknown name — @totalNonsense
        // parses cleanly — so this list is the only place a typo is caught.
        assertEquals(
            setOf("viewModel", "masterPage", "baseType", "resourceType", "resourceNamespace",
                  "import", "wrapperTag", "noWrapperTag", "service", "js", "property"),
            DirectiveScanner.KNOWN_NAMES.toSet())
    }

    @Test fun recognisesTheDirectivesThatWereMissing() {
        val names = DirectiveScanner.scan("@wrapperTag div\n@resourceType Default\n<html></html>")
            .map { it.name }
        assertEquals(listOf("wrapperTag", "resourceType"), names)
    }

    @Test fun doesNotRecogniseViewModule() {
        // There is no `viewModule` directive in DotVVM; the one that exists is `js`
        assertTrue(DirectiveScanner.scan("@viewModule Foo.js\n<html></html>").isEmpty())
    }

    @Test fun theCaretOnTheNameIsOnTheName() {
        assertTrue(DirectiveScanner.isOnName("@view\n<html></html>", 5))
        assertTrue(DirectiveScanner.isOnName("@viewModel App.Vm", 4))
        assertTrue(DirectiveScanner.isOnName("@viewModel App.Vm", 10))
    }

    @Test fun theCaretInTheValueIsNotOnTheName() {
        // This is what used to offer every directive name in the middle of a value
        assertFalse(DirectiveScanner.isOnName("@viewModel App.Vm", 11))
        assertFalse(DirectiveScanner.isOnName("@viewModel ", 11))
    }

    @Test fun anEmptyLineCanStillBecomeADirective() {
        assertTrue(DirectiveScanner.isOnName("\n<html></html>", 0))
    }

    @Test fun theCaretInMarkupIsNotOnAName() {
        assertFalse(DirectiveScanner.isOnName("<html>@x</html>", 7))
    }

    @Test fun handlesAnIndentedDirective() {
        assertTrue(DirectiveScanner.isOnName("   @view", 8))
        assertFalse(DirectiveScanner.isOnName("   @viewModel App", 15))
    }
}
