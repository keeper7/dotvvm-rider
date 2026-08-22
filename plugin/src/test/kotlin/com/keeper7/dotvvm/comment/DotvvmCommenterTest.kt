package com.keeper7.dotvvm.comment

import com.intellij.testFramework.fixtures.BasePlatformTestCase

class DotvvmCommenterTest : BasePlatformTestCase() {

    fun testCommentActionProducesServerSideComment() {
        myFixture.configureByText("Test.dothtml", "<div><selection>x</selection></div>")
        myFixture.performEditorAction("CommentByBlockComment")
        myFixture.checkResult("<div><%--x--%></div>")
    }

    fun testLineCommentAlsoUsesBlockForm() {
        // DotVVM has no line comment, so the platform has to fall back to the block form
        myFixture.configureByText("Test.dothtml", "<div>\n<caret><span>x</span>\n</div>")
        myFixture.performEditorAction("CommentByLineComment")
        assertTrue(
            "Got: " + myFixture.editor.document.text,
            myFixture.editor.document.text.contains("<%--"))
    }

    fun testCommentingIsReversible() {
        // Uncommenting is what tells the platform our form apart from the HTML one
        myFixture.configureByText("Test.dothtml", "<div><selection>x</selection></div>")
        myFixture.performEditorAction("CommentByBlockComment")
        myFixture.performEditorAction("CommentByBlockComment")
        myFixture.checkResult("<div>x</div>")
    }
}
