using DotVVM.LanguageServer.Model;

namespace DotVVM.LanguageServer.Analysis;

/// <summary>What kind of thing a suggestion is, so the handler can pick an LSP item kind.</summary>
public enum SuggestionKind { Prefix, Tag, Property }

/// <summary>
/// One completion suggestion, free of protocol types. InsertText carries a $0 placeholder when
/// IsSnippet is set; a client without snippet support gets the text with the placeholder removed.
/// </summary>
public record CompletionSuggestion(
    string Label,
    SuggestionKind Kind,
    string InsertText,
    bool IsSnippet = false,
    string? Detail = null,
    string? SortText = null);

/// <summary>
/// Decides what may be written where the caret is. Kept out of the handler so the decision can
/// be tested without speaking the protocol - the same split as ControlHoverText.
/// </summary>
public static class ControlCompletion
{
    public static IReadOnlyList<CompletionSuggestion> Suggest(
        ControlRegistry registry, CompletionContext context) =>
        context.Target switch
        {
            CompletionTarget.TagPrefix => Prefixes(registry),
            CompletionTarget.TagName => Tags(registry, context.Prefix),
            CompletionTarget.AttributeName => Attributes(registry, context),
            _ => Array.Empty<CompletionSuggestion>(),
        };

    private static IReadOnlyList<CompletionSuggestion> Prefixes(ControlRegistry registry) =>
        registry.AllPrefixes
            .Select(p => new CompletionSuggestion(p, SuggestionKind.Prefix, p + ":",
                                                  Detail: "DotVVM tag prefix"))
            .ToList();

    private static IReadOnlyList<CompletionSuggestion> Tags(ControlRegistry registry, string? prefix)
    {
        if (prefix is null || !registry.IsKnownPrefix(prefix))
        {
            return Array.Empty<CompletionSuggestion>();
        }

        return registry.GetTagsForPrefix(prefix)
            .Select(t => new CompletionSuggestion(t, SuggestionKind.Tag, t, Detail: $"{prefix}:{t}"))
            .ToList();
    }

    private static IReadOnlyList<CompletionSuggestion> Attributes(
        ControlRegistry registry, CompletionContext context)
    {
        var written = new HashSet<string>(
            context.WrittenAttributes ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);

        // A plain HTML element has no prefix, and its own attributes belong to the IDE's HTML
        // support. Only the attached properties are ours to offer there - and they are offered
        // everywhere, which is the whole point of being attached.
        var own = context.Prefix is null
            ? Array.Empty<ControlProperty>()
            : registry.GetControl(context.Prefix, context.TagName ?? "")?.Properties.ToArray()
              ?? Array.Empty<ControlProperty>();

        return own.Select(p => (Property: p, Attached: false))
            .Concat(registry.AttachedProperties.Select(p => (Property: p, Attached: true)))
            .Where(p => p.Property.IsAttribute && !written.Contains(p.Property.Name))
            .Select(p => Describe(p.Property, p.Attached, context.EditedAttributeHasValue))
            .ToList();
    }

    private static CompletionSuggestion Describe(
        ControlProperty property, bool attached, bool nameOnly)
    {
        // Renaming an attribute that already has a value: bringing one of our own would leave
        // the old one standing behind it. A property that takes nothing but a binding is worth
        // writing the braces for; anything else gets an empty value with the caret inside.
        var insertText = nameOnly ? property.Name
            : property.Value == PropertyValue.BindingOnly
                ? $"{property.Name}=\"{{value: $0}}\""
                : $"{property.Name}=\"$0\"";

        return new CompletionSuggestion(
            Label: property.Name,
            Kind: SuggestionKind.Property,
            InsertText: insertText,
            IsSnippet: !nameOnly,
            Detail: property.TypeName is null ? null : ShortTypeName(property.TypeName),
            // Required first, then the control's own, then the attached ones - which apply to
            // every element and would otherwise sit in the middle of the control's own list.
            // The digit never reaches the user.
            SortText: (property.Required ? "0" : attached ? "2" : "1") + property.Name);
    }

    private static string ShortTypeName(string fullName)
    {
        var withoutAssembly = fullName.Split(',')[0];
        var lastDot = withoutAssembly.LastIndexOf('.');
        return lastDot < 0 ? withoutAssembly : withoutAssembly[(lastDot + 1)..];
    }
}
