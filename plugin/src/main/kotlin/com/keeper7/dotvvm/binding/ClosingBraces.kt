package com.keeper7.dotvvm.binding

/**
 * Whether a binding that has just been opened needs its closing braces written for it.
 *
 * Free of IntelliJ API so a plain JUnit test reaches it: what is hard here is not the insertion
 * but the decision, and the decision must never touch a binding that is already closed.
 */
object ClosingBraces {

    /**
     * True when the two characters before the caret are the `{{` the author has just written and
     * nothing closes them yet.
     *
     * What follows the caret settles it: a brace of any kind means the binding is already closed
     * or another one begins, and the end of the line, the end of an attribute value or the start
     * of a tag mean there is nothing there to close it.
     */
    fun needed(text: CharSequence, offset: Int): Boolean {
        if (offset < 2 || offset > text.length) return false
        if (text[offset - 1] != '{' || text[offset - 2] != '{') return false

        // A third brace is not a binding being opened, whatever it is
        if (offset >= 3 && text[offset - 3] == '{') return false

        for (i in offset until text.length) {
            when (text[i]) {
                '}' -> return false
                '{' -> return true
                '\n', '<', '"', '\'' -> return true
            }
        }
        return true
    }
}
