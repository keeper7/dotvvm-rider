package com.keeper7.dotvvm.binding

enum class BindingKind { SINGLE, DOUBLE }

data class BindingMatch(val start: Int, val end: Int, val kind: BindingKind)

/**
 * Finds binding expressions in text. Unlike a regex it handles nested braces and braces
 * inside string literals correctly.
 *
 * Free of IntelliJ API, so it is testable with a plain JUnit test.
 */
object BindingScanner {

    private val KNOWN_KINDS = setOf(
        "value", "command", "staticCommand", "resource",
        "controlProperty", "controlCommand", "_control", "_parent", "_root"
    )

    fun scan(text: String): List<BindingMatch> {
        val result = mutableListOf<BindingMatch>()
        var i = 0
        while (i < text.length) {
            if (text[i] != '{') { i++; continue }

            val isDouble = i + 1 < text.length && text[i + 1] == '{'
            val contentStart = if (isDouble) i + 2 else i + 1
            if (!hasKnownKeyword(text, contentStart)) { i++; continue }

            val end = findEnd(text, contentStart, isDouble)
            if (end < 0) { i++; continue }

            result.add(BindingMatch(i, end, if (isDouble) BindingKind.DOUBLE else BindingKind.SINGLE))
            i = end
        }
        return result
    }

    /** Checks that a known keyword followed by a colon comes after the opening brace. */
    private fun hasKnownKeyword(text: String, from: Int): Boolean {
        var j = from
        while (j < text.length && text[j].isWhitespace()) j++
        val start = j
        while (j < text.length && (text[j].isLetterOrDigit() || text[j] == '_')) j++
        if (j == start) return false
        val keyword = text.substring(start, j)
        while (j < text.length && text[j].isWhitespace()) j++
        return j < text.length && text[j] == ':' && keyword in KNOWN_KINDS
    }

    /**
     * Returns the index past the end of the binding, or -1 when the binding is unterminated.
     * Tracks brace depth and whether the scan is inside a string literal.
     */
    private fun findEnd(text: String, contentStart: Int, isDouble: Boolean): Int {
        var depth = 1
        var i = contentStart
        var quote: Char? = null

        while (i < text.length) {
            val c = text[i]
            when {
                quote != null -> {
                    if (c == '\\') i++            // escape sequence: skip the next character
                    else if (c == quote) quote = null
                }
                c == '"' || c == '\'' -> quote = c
                c == '{' -> depth++
                c == '}' -> {
                    depth--
                    if (depth == 0) {
                        return if (isDouble) {
                            if (i + 1 < text.length && text[i + 1] == '}') i + 2 else -1
                        } else i + 1
                    }
                }
            }
            i++
        }
        return -1
    }
}
