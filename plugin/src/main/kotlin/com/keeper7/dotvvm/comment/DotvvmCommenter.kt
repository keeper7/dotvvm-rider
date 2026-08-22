package com.keeper7.dotvvm.comment

import com.intellij.lang.Commenter

/**
 * DotVVM has only the server-side comment, and that is the one a user wants here: unlike
 * `<!-- -->` it never reaches the browser. There is no line comment form, so the platform
 * falls back to the block form for the line-comment action too.
 */
class DotvvmCommenter : Commenter {

    override fun getLineCommentPrefix(): String? = null

    override fun getBlockCommentPrefix(): String = "<%--"

    override fun getBlockCommentSuffix(): String = "--%>"

    override fun getCommentedBlockCommentPrefix(): String? = null

    override fun getCommentedBlockCommentSuffix(): String? = null
}
