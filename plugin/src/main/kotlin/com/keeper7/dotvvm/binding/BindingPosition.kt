package com.keeper7.dotvvm.binding

/**
 * Where the caret stands inside a binding: the kind is still being written (`{{va`), or it is
 * already there and an expression follows (`{{value: Cus`).
 */
data class BindingPlace(val kindWritten: Boolean)

/**
 * Whether the caret is inside a binding at all, and how far into it.
 *
 * The server decides the same thing for its own answers, and this is deliberately the same walk:
 * the plugin cannot ask the server, because it needs the answer *before* the request goes out —
 * to keep HTML tags out of the list, and to select an item so that Tab inserts it rather than
 * reaching Emmet.
 *
 * Free of IntelliJ API, so a plain JUnit test reaches it — the same split as [BindingScanner],
 * which answers a different question: that one finds the bindings that are **finished**, this
 * one the unfinished binding under the caret.
 */
object BindingPosition {

    /** The kinds DotVVM knows. The two control bindings belong to a markup control's file. */
    val KINDS = listOf(
        "value", "command", "staticCommand", "resource", "controlProperty", "controlCommand"
    )

    private val MARKUP_CONTROL_ONLY = setOf("controlProperty", "controlCommand")

    /** What a file of this name may bind to; in a page a control binding does not compile. */
    fun kindsFor(fileName: String): List<String> =
        if (fileName.endsWith(".dotcontrol", ignoreCase = true)) KINDS
        else KINDS.filterNot { it in MARKUP_CONTROL_ONLY }

    fun at(text: String, offset: Int): BindingPlace? {
        val caret = offset.coerceIn(0, text.length)
        var i = 0

        while (i < caret) {
            // A region that swallows the caret pushes the cursor past it, and the walk ends
            // with nothing found — the right answer inside a comment or a script
            val skipped = skipRegion(text, i)
            if (skipped > i) { i = skipped; continue }

            if (text[i] != '{') { i++; continue }

            // `{{` counts as one opening only once the caret has passed both braces; between
            // them the second one is not there yet as far as the author knows
            val double = i + 1 < text.length && text[i + 1] == '{' && caret > i + 1
            val contentStart = i + if (double) 2 else 1

            val end = findEnd(text, contentStart, double)
            if (end in 0..caret) { i = end; continue }

            describe(text.substring(contentStart, caret))?.let { return it }
            i++
        }

        return null
    }

    private fun describe(content: String): BindingPlace? {
        // The kind's colon is always the first one: an expression's own colons come later
        val colon = content.indexOf(':')
        if (colon < 0) {
            val word = content.trimStart()
            return if (isIdentifier(word)) BindingPlace(kindWritten = false) else null
        }

        val kind = content.substring(0, colon).trim()
        return if (kind in KINDS) BindingPlace(kindWritten = true) else null
    }

    /**
     * The index past the binding's end, or -1 while it is unterminated — which is the ordinary
     * state of the one being written. Braces nest, and a brace inside a string literal is not one.
     */
    private fun findEnd(text: String, contentStart: Int, double: Boolean): Int {
        var depth = 1
        var quote: Char? = null
        var i = contentStart

        while (i < text.length) {
            val c = text[i]
            when {
                quote != null -> {
                    if (c == '\\') i++ else if (c == quote) quote = null
                }
                c == '"' || c == '\'' -> quote = c
                c == '{' -> depth++
                c == '}' -> {
                    depth--
                    if (depth == 0) {
                        return if (double) {
                            if (i + 1 < text.length && text[i + 1] == '}') i + 2 else -1
                        } else i + 1
                    }
                }
            }
            i++
        }
        return -1
    }

    private fun skipRegion(text: String, at: Int): Int {
        var end = skip(text, at, "<!--", "-->")
        if (end > at) return end

        end = skip(text, at, "<%--", "--%>")
        if (end > at) return end

        end = skipElement(text, at, "script")
        if (end > at) return end

        return skipElement(text, at, "style")
    }

    /** The index past the region, or the index given when it does not start there. */
    private fun skip(text: String, at: Int, open: String, close: String): Int {
        if (!text.startsWith(open, at)) return at

        val end = text.indexOf(close, at)
        return if (end < 0) text.length else end + close.length
    }

    /**
     * Skips a script or a style element whole: braces are the ordinary punctuation of both
     * languages, and `{ color: red }` is not a binding however much the shape resembles one.
     */
    private fun skipElement(text: String, at: Int, name: String): Int {
        if (!text.regionMatches(at, "<$name", 0, name.length + 1, ignoreCase = true)) return at

        val after = at + name.length + 1
        if (after < text.length && !text[after].isWhitespace() &&
            text[after] != '>' && text[after] != '/') {
            return at
        }

        val end = text.indexOf("</$name", at, ignoreCase = true)
        return if (end < 0) text.length else end + name.length + 2
    }

    private fun isIdentifier(word: String) =
        word.isEmpty() || (!word[0].isDigit() && word.all { it.isLetterOrDigit() || it == '_' })
}
