package com.keeper7.dotvvm.lsp

import com.intellij.openapi.vfs.VirtualFile
import com.keeper7.dotvvm.lang.DotControlFileType
import com.keeper7.dotvvm.lang.DotHtmlFileType
import com.keeper7.dotvvm.lang.DotMasterFileType

/**
 * Server se smí spustit jen pro DotVVM projekty. Rozhoduje o tom typ otevřeného souboru,
 * protože jiný spolehlivý znak DotVVM projektu neexistuje — `.csproj` může odkazovat
 * DotVVM tranzitivně a nemusí být otevřený vůbec.
 */
internal fun isDotvvmFile(file: VirtualFile): Boolean =
    file.fileType == DotHtmlFileType.INSTANCE ||
    file.fileType == DotControlFileType.INSTANCE ||
    file.fileType == DotMasterFileType.INSTANCE
