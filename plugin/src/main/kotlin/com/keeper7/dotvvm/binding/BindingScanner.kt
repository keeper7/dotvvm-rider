package com.keeper7.dotvvm.binding

enum class BindingKind { SINGLE, DOUBLE }

data class BindingMatch(val start: Int, val end: Int, val kind: BindingKind)

/**
 * Najde binding výrazy v textu. Na rozdíl od regexu správně zpracuje
 * vnořené složené závorky a složené závorky uvnitř řetězcových literálů.
 *
 * Bez závislosti na IntelliJ API — testovatelné obyčejným JUnit testem.
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

    /** Ověří, že za otevírací závorkou následuje známé klíčové slovo a dvojtečka. */
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
     * Vrátí index za koncem bindingu, nebo -1 když binding není ukončený.
     * Sleduje hloubku závorek a stav řetězcového literálu.
     */
    private fun findEnd(text: String, contentStart: Int, isDouble: Boolean): Int {
        var depth = 1
        var i = contentStart
        var quote: Char? = null

        while (i < text.length) {
            val c = text[i]
            when {
                quote != null -> {
                    if (c == '\\') i++            // escape sekvence — přeskoč další znak
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
