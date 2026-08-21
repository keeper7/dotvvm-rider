package com.keeper7.dotvvm.directive

/** Jedna direktiva i s rozsahem v textu souboru. */
data class Directive(val name: String, val value: String, val start: Int, val end: Int)

/**
 * Najde direktivy na začátku souboru.
 *
 * Bez závislosti na IntelliJ API, aby šel otestovat obyčejným JUnit testem — stejné
 * dělení odpovědnosti jako u [com.keeper7.dotvvm.binding.BindingScanner]: skener říká
 * *kde* direktivy jsou, lexer *jak* je předat platformě.
 */
object DirectiveScanner {

    /**
     * Direktivy, které DotVVM zná. Neznámé jméno se za direktivu nepovažuje — jinak by
     * skener spolkl libovolný text začínající zavináčem a schoval ho před HTML parserem.
     */
    val KNOWN_NAMES = listOf(
        "viewModel", "masterPage", "import", "service", "js",
        "baseType", "property", "noWrapperTag", "viewModule"
    )

    /** Značka pořadí bajtů na začátku souborů uložených Visual Studiem. */
    private const val BOM = '\uFEFF'

    fun scan(text: String): List<Directive> {
        val result = mutableListOf<Directive>()
        var offset = 0

        while (offset < text.length) {
            val lineEnd = text.indexOf('\n', offset).let { if (it < 0) text.length else it }
            val line = text.substring(offset, lineEnd)
            val trimmedLine = line.trimEnd('\r')
            val content = trimmedLine.dropWhile { it.isWhitespace() || it == BOM }
            val indent = trimmedLine.length - content.length

            when {
                content.isEmpty() -> {
                    // Prázdné řádky mezi direktivami blok neukončují
                }
                content.startsWith('<') -> return result   // začalo tělo dokumentu
                content.startsWith('@') -> {
                    val directive = parseLine(content, offset + indent) ?: return result
                    result.add(directive)
                }
                else -> return result                      // cokoli jiného blok ukončuje
            }

            offset = lineEnd + 1
        }

        return result
    }

    private fun parseLine(content: String, start: Int): Directive? {
        val nameEnd = content.indexOfFirst { it.isWhitespace() }
            .let { if (it < 0) content.length else it }
        val name = content.substring(1, nameEnd)
        if (name !in KNOWN_NAMES) return null

        val value = content.substring(nameEnd).trim()
        return Directive(name, value, start, start + content.trimEnd().length)
    }
}
