package com.keeper7.dotvvm.lsp

import com.intellij.openapi.components.Service
import com.intellij.openapi.components.service
import com.intellij.openapi.project.Project
import java.util.concurrent.atomic.AtomicReference

/**
 * One entry of `config.markup.controls`: either a whole namespace (namespace + assembly) or a
 * single markup control registered by file (tagName + src).
 */
data class ControlRegistration(
    val prefix: String,
    val tagName: String?,
    val src: String?,
    val namespace: String?,
    val assembly: String?,
) {
    val isMarkupControl: Boolean get() = tagName != null && src != null
}

/**
 * What the server knows about the project's control registrations, kept on the client side.
 *
 * The plugin needs them to navigate out of a tag, and navigating out of a tag has to happen
 * here: the platform asks an LSP server only where an element carries no reference of its own,
 * and an `XmlTag` carries one — that self-reference is what underlines the tag and then leads
 * nowhere. Since the plugin cannot work the registrations out for itself, the server sends
 * them along with the configuration tier.
 */
@Service(Service.Level.PROJECT)
class ControlRegistrations {

    private val registrations = AtomicReference<List<ControlRegistration>>(emptyList())

    fun update(value: List<ControlRegistration>) = registrations.set(value)

    val all: List<ControlRegistration> get() = registrations.get()

    /** The file-registered control behind `prefix:TagName`, if the tag is one. */
    fun markupControl(prefix: String, tagName: String): ControlRegistration? =
        registrations.get().firstOrNull {
            it.prefix == prefix && it.tagName == tagName && it.src != null
        }

    /**
     * The namespaces the prefix stands for. One prefix can register several — a project
     * routinely puts `cc` on both its own controls and a library's — so the caller tries them
     * in turn rather than taking the first.
     */
    fun namespaces(prefix: String): List<ControlRegistration> =
        registrations.get().filter { it.prefix == prefix && it.namespace != null }

    companion object {
        fun of(project: Project): ControlRegistrations = project.service()
    }
}
