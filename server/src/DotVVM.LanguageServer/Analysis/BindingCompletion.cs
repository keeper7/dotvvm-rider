namespace DotVVM.LanguageServer.Analysis;

/// <summary>
/// What may stand right after an opening brace. Free of protocol types, the same split as
/// <see cref="ControlCompletion"/> and <see cref="DirectiveCompletion"/>.
///
/// The kinds are the framework's own and cannot be read off the registry the way tags and
/// properties are: nothing in a project declares them, and a project cannot add one.
/// </summary>
public static class BindingCompletion
{
    /// <summary>
    /// controlProperty and controlCommand bind to the properties of the markup control the file
    /// declares, so they are offered in a .dotcontrol and nowhere else - in a page they compile
    /// to "control property binding used outside a markup control".
    /// </summary>
    private static readonly string[] MarkupControlOnly = ["controlProperty", "controlCommand"];

    private static readonly Dictionary<string, string> Details = new(StringComparer.Ordinal)
    {
        ["value"] = "a value of the view model, updated in both directions",
        ["command"] = "a method called on the server",
        ["staticCommand"] = "a method called without a postback",
        ["resource"] = "a value rendered once, on the server",
        ["controlProperty"] = "a property of this markup control",
        ["controlCommand"] = "a command of this markup control",
    };

    public static IReadOnlyList<CompletionSuggestion> Kinds(string? filePath)
    {
        var markupControl = filePath is not null &&
                            filePath.EndsWith(".dotcontrol", StringComparison.OrdinalIgnoreCase);

        return BindingContextScanner.Kinds
            .Where(kind => markupControl || !MarkupControlOnly.Contains(kind))
            // Sorted by how often each is written rather than alphabetically, which would put
            // `command` in front of `value` - the one meant nine times out of ten.
            .Select((kind, order) => new CompletionSuggestion(
                Label: kind + ":",
                Kind: SuggestionKind.Binding,
                InsertText: kind + ":",
                Detail: Details.GetValueOrDefault(kind),
                SortText: order.ToString("D2")))
            .ToList();
    }
}
