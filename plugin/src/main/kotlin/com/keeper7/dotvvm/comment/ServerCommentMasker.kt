package com.keeper7.dotvvm.comment

/**
 * Hands the HTML lexer a DotVVM server-side comment it can understand.
 *
 * Outside a tag `<%--` becomes `<!--` and `--%>` becomes a space followed by `-->`, so the
 * lexer sees an ordinary HTML comment and both the colouring and the parsing come for free —
 * the contents stay out of the tree, so a control commented out this way is no longer parsed
 * as markup.
 *
 * The padding space goes **before** the closer, not after it. With `--> ` the comment element
 * ended one character early and the final `>` fell out of it as whitespace, leaving it
 * unpainted in the editor — visible on the very first line the user looks at.
 *
 * **Between attributes there is no such trick.** HTML knows no comment inside a tag, so `<!--`
 * there reads as three more attributes and the tag never closes — the rest of the file falls
 * apart with it. DotVVM does allow the form (verified against its own tokenizer: `<th <%-- … --%>>`
 * parses with no error and keeps the attributes around it), so the comment is blanked out
 * instead. The lexer then sees a plain tag; `ServerCommentAnnotator` puts the colour back.
 *
 * Every replacement is character for character, so the length and every offset in the file
 * still match. Free of IntelliJ API so it can be tested with a plain JUnit test.
 */
object ServerCommentMasker {

    private const val OPEN = "<%--"
    private const val CLOSE = "--%>"
    private const val MASKED_OPEN = "<!--"
    private const val MASKED_CLOSE = " -->"

    /** One server-side comment: where it is, and whether it sits between a tag's attributes. */
    data class Comment(val start: Int, val end: Int, val insideTag: Boolean)

    /** Returns the original text when there is nothing to mask, to avoid a needless copy. */
    fun mask(text: CharSequence): CharSequence {
        val comments = scan(text)
        if (comments.isEmpty()) return text

        val result = StringBuilder(text)
        for (comment in comments) {
            if (comment.insideTag) {
                // Line breaks stay: blanking them would shift every line number after the
                // comment, and LSP diagnostics are addressed by line and column
                for (i in comment.start until comment.end) {
                    if (result[i] != '\n' && result[i] != '\r') result.setCharAt(i, ' ')
                }
            } else {
                result.replace(comment.start, comment.start + OPEN.length, MASKED_OPEN)
                val closeAt = comment.end - CLOSE.length
                if (closeAt >= comment.start + OPEN.length && matches(text, closeAt, CLOSE)) {
                    result.replace(closeAt, comment.end, MASKED_CLOSE)
                }
            }
        }
        return result
    }

    /**
     * Finds every server-side comment, walking the text forward so that it knows whether the
     * caret of the moment stands inside a tag. Searching backwards from each `<%--` cannot tell:
     * a `>` before it may just as well have come from an attribute value.
     */
    fun scan(text: CharSequence): List<Comment> {
        val result = mutableListOf<Comment>()
        var i = 0
        var insideTag = false

        while (i < text.length) {
            if (matches(text, i, OPEN)) {
                val close = indexOf(text, CLOSE, i + OPEN.length)
                val end = if (close < 0) text.length else close + CLOSE.length
                result.add(Comment(i, end, insideTag))
                i = end
                continue
            }

            val c = text[i]
            if (!insideTag) {
                // `<!` and `<?` do not open a tag, and neither does a bare `<` in text
                if (c == '<' && i + 1 < text.length &&
                    (text[i + 1].isLetter() || text[i + 1] == '/')) insideTag = true
                i++
                continue
            }

            when {
                c == '>' -> { insideTag = false; i++ }
                // A quoted value may hold anything, `>` and `<%--` included
                c == '"' || c == '\'' -> {
                    val end = indexOf(text, c.toString(), i + 1)
                    i = if (end < 0) text.length else end + 1
                }
                else -> i++
            }
        }
        return result
    }

    private fun matches(text: CharSequence, at: Int, what: String): Boolean {
        if (at + what.length > text.length) return false
        for (j in what.indices) if (text[at + j] != what[j]) return false
        return true
    }

    /** Searches a CharSequence, which String.indexOf cannot do without copying it first. */
    private fun indexOf(text: CharSequence, needle: String, from: Int): Int {
        var i = from
        val last = text.length - needle.length
        while (i <= last) {
            if (matches(text, i, needle)) return i
            i++
        }
        return -1
    }
}
