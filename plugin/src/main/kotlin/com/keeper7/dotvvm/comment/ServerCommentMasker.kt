package com.keeper7.dotvvm.comment

/**
 * Hands the HTML lexer a DotVVM server-side comment it can understand.
 *
 * `<%--` becomes `<!--` and `--%>` becomes a space followed by `-->`, so the replacement is
 * character for character and every offset in the file still matches. The lexer then sees an
 * ordinary HTML comment, which gives both the colouring and the parsing for free — the contents
 * stay out of the tree, so a control commented out this way is no longer parsed as markup.
 *
 * The padding space goes **before** the closer, not after it. With `--> ` the comment element
 * ended one character early and the final `>` fell out of it as whitespace, leaving it unpainted
 * in the editor — visible on the very first line the user looks at.
 *
 * Free of IntelliJ API so it can be tested with a plain JUnit test.
 */
object ServerCommentMasker {

    private const val OPEN = "<%--"
    private const val CLOSE = "--%>"
    private const val MASKED_OPEN = "<!--"
    private const val MASKED_CLOSE = " -->"

    /** Returns the original text when there is nothing to mask, to avoid a needless copy. */
    fun mask(text: CharSequence): CharSequence {
        var from = indexOf(text, OPEN, 0)
        if (from < 0) return text

        val result = StringBuilder(text)
        while (from >= 0) {
            result.replace(from, from + OPEN.length, MASKED_OPEN)

            val close = indexOf(result, CLOSE, from + OPEN.length)
            val continueFrom = if (close < 0) {
                result.length                          // unterminated: the rest is the comment
            } else {
                result.replace(close, close + CLOSE.length, MASKED_CLOSE)
                close + CLOSE.length
            }

            from = indexOf(result, OPEN, continueFrom)
        }
        return result
    }

    /** Searches a CharSequence, which String.indexOf cannot do without copying it first. */
    private fun indexOf(text: CharSequence, needle: String, from: Int): Int {
        var i = from
        val last = text.length - needle.length
        outer@ while (i <= last) {
            for (j in needle.indices) {
                if (text[i + j] != needle[j]) { i++; continue@outer }
            }
            return i
        }
        return -1
    }
}
