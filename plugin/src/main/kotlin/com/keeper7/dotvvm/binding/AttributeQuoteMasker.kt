package com.keeper7.dotvvm.binding

/**
 * Skryje před HTML lexerem uvozovky, které jsou uvnitř binding výrazu.
 *
 * DotVVM zápis `Changed="{staticCommand: X = Y ?? ""}"` je platný, ale HTML uvozovku uvnitř
 * hodnoty nepřipouští — lexer by hodnotu ukončil u `??` a zbytek výrazu i další atributy by
 * vypadly z tagu. Řídit stav HTML lexeru zvenčí nelze, zato mu lze podstrčit text, ve kterém
 * problém není: uvozovky uvnitř `{…}` se nahradí mezerou.
 *
 * Náhrada je **znak za znak**, takže délka i všechny offsety zůstávají shodné s originálem
 * a tokeny ukazují na správná místa v souboru.
 *
 * Bez závislosti na IntelliJ API, aby šel otestovat obyčejným JUnit testem.
 */
object AttributeQuoteMasker {

    /** Vrátí původní text, pokud maskovat není co — ať se nekopíruje zbytečně. */
    fun mask(text: CharSequence): CharSequence {
        var result: StringBuilder? = null
        var i = 0

        while (i < text.length) {
            if (text[i] != '=') { i++; continue }

            val quote = skipSpaces(text, i + 1)
            if (quote >= text.length || text[quote] != '"') { i++; continue }
            if (quote + 1 >= text.length || text[quote + 1] != '{') { i = quote + 1; continue }

            // Uvnitř hodnoty: uvozovky ve vnořeném bindingu se maskují
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
