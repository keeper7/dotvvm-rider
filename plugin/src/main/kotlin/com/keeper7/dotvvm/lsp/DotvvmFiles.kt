package com.keeper7.dotvvm.lsp

import com.intellij.openapi.vfs.VirtualFile
import com.keeper7.dotvvm.lang.DotControlFileType
import com.keeper7.dotvvm.lang.DotHtmlFileType
import com.keeper7.dotvvm.lang.DotMasterFileType

/**
 * The server may start only for DotVVM projects. The type of the opened file decides, because
 * there is no other reliable sign of one: a `.csproj` may reference DotVVM transitively and need
 * not be open at all.
 */
internal fun isDotvvmFile(file: VirtualFile): Boolean =
    file.fileType == DotHtmlFileType.INSTANCE ||
    file.fileType == DotControlFileType.INSTANCE ||
    file.fileType == DotMasterFileType.INSTANCE
