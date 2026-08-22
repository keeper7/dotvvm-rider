package com.keeper7.dotvvm.directive

/** Jedna direktiva i s rozsahem v textu souboru. */
data class Directive(val name: String, val value: String, val start: Int, val end: Int)

/**
 * Finds the directives at the start of the file.
 *
 * Free of IntelliJ API so it can be tested with a plain JUnit test — the same split of
 * responsibility as in [com.keeper7.dotvvm.binding.BindingScanner]: the scanner says *where*
 * the directives are, the lexer *how* to hand them to the platform.
 */
object DirectiveScanner {

    /**
     * The directives DotVVM knows. An unknown name is not treated as a directive; otherwise
     * the scanner would swallow any text starting with an at sign and hide it from the parser.
     */
    val KNOWN_NAMES = listOf(
        "viewModel", "masterPage", "import", "service", "js",
        "baseType", "property", "noWrapperTag", "viewModule"
    )

    /** Byte order mark at the start of files saved by Visual Studio. */
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
                    // Blank lines between directives do not end the block
                }
                content.startsWith('<') -> return result   // the document body has started
                content.startsWith('@') -> {
                    val directive = parseLine(content, offset + indent) ?: return result
                    result.add(directive)
                }
                else -> return result                      // anything else ends the block
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
