package com.keeper7.dotvvm.directive

import org.junit.Assert.assertEquals
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
        // Po prvním tagu už direktivy nejsou — @something uvnitř těla je obyčejný text
        val text = "@viewModel App.Vm\n<html>\n@viewModel App.Other\n</html>"
        val found = DirectiveScanner.scan(text)
        assertEquals(1, found.size)
    }

    @Test fun stopsAtDoctype() {
        val text = "@viewModel App.Vm\n<!DOCTYPE html>\n<html></html>"
        assertEquals(1, DirectiveScanner.scan(text).size)
    }

    @Test fun ignoresUnknownDirectiveName() {
        // Neznámé jméno není direktiva; skener nesmí pohltit cizí text
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
        // Skener hodnotu nerozděluje na typ a assembly — to dělá až server
        // (FindAssemblySeparator ve ViewModelDirective.cs). Tady jde jen o to, že
        // čárky ani lomené závorky direktivu předčasně neukončí.
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
        // Soubory uložené Visual Studiem začínají BOM; bez jeho přeskočení
        // by skener neviděl ani první direktivu
        val text = "\uFEFF@viewModel System.Object\n@noWrapperTag\n<div></div>"
        val found = DirectiveScanner.scan(text)
        assertEquals(listOf("viewModel", "noWrapperTag"), found.map { it.name })
        assertEquals(1, found[0].start)
    }

    @Test fun knownNamesContainViewModel() {
        assertTrue(DirectiveScanner.KNOWN_NAMES.contains("viewModel"))
    }
}
