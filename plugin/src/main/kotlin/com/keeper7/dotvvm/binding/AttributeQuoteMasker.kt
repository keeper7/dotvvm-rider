package com.keeper7.dotvvm.binding

/**
 * Hides from the HTML lexer the quotes that sit inside a binding expression.
 *
 * The DotVVM form `Changed="{staticCommand: X = Y ?? ""}"` is valid, but HTML does not allow
 * a quote inside a value — the lexer would end the value at `??`, and the rest of the
 * expression along with the following attributes would fall out of the tag. The HTML lexer's
 * state cannot be steered from outside, but it can be handed text without the problem:
 * quotes inside `{…}` are replaced with a space.
 *
 * The replacement is **character for character**, so the length and every offset stay equal
 * to the original and the tokens still point at the right places in the file.
 *
 * Free of IntelliJ API so it can be tested with a plain JUnit test.
 */
object AttributeQuoteMasker {

    /** Returns the original text when there is nothing to mask, to avoid a needless copy. */
    fun mask(text: CharSequence): CharSequence {
        var result: StringBuilder? = null
        var i = 0

        while (i < text.length) {
            if (text[i] != '=') { i++; continue }

            val quote = skipSpaces(text, i + 1)
            if (quote >= text.length || text[quote] != '"') { i++; continue }
            if (quote + 1 >= text.length || text[quote + 1] != '{') { i = quote + 1; continue }

            // Inside the value: quotes within the nested binding get masked
            var depth = 0
            var j = quote + 1
            while (j < text.length) {
                when (text[j]) {
                    '{' -> depth++
                    '}' -> depth--
                    '\n' -> if (depth <= 0) break
                    '"' -> {
                        if (depth <= 0) break
                        val sb = result ?: StringBuilder(text).also { result = it }
                        sb.setCharAt(j, ' ')
                    }
                }
                if (text[j] == '"' && depth <= 0) break
                if (text[j] == '\n' && depth <= 0) break
                j++
            }
            i = j + 1
        }

        return result ?: text
    }

    private fun skipSpaces(text: CharSequence, from: Int): Int {
        var i = from
        while (i < text.length && (text[i] == ' ' || text[i] == '\t')) i++
        return i
    }
}
