using DotVVM.LanguageServer.Analysis;
using DotVVM.LanguageServer.Configuration;
using DotVVM.LanguageServer.Documents;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace DotVVM.LanguageServer.Handlers;

public class CompletionHandler : ICompletionHandler
{
    private readonly DocumentStore _documents;
    private readonly ProjectConfigurationProvider _configuration;

    public CompletionHandler(DocumentStore documents, ProjectConfigurationProvider configuration)
    {
        _documents = documents;
        _configuration = configuration;
    }

    /// <summary>
    /// Whether the client can handle a $0 placeholder. Captured while registering, because
    /// Handle does not see the capabilities and inserting "$0" literally would be worse than
    /// inserting nothing.
    /// </summary>
    private bool _snippets;

    public CompletionRegistrationOptions GetRegistrationOptions(
        CompletionCapability capability, ClientCapabilities clientCapabilities)
    {
        // The capability itself can be absent: a client that does not ask for completion at all
        // still gets its registration options read, and dereferencing it there killed the whole
        // initialize handshake - the server never came up, for want of a question mark.
        _snippets = capability?.CompletionItem?.SnippetSupport ?? false;

        return new CompletionRegistrationOptions
        {
            DocumentSelector = new TextDocumentSelector(
                new TextDocumentFilter { Pattern = "**/*.dothtml" },
                new TextDocumentFilter { Pattern = "**/*.dotmaster" },
                new TextDocumentFilter { Pattern = "**/*.dotcontrol" }),
            // The space is what opens the attribute list without the user asking for it
            TriggerCharacters = new Container<string>("<", ":", " ")
        };
    }

    public async Task<CompletionList> Handle(CompletionParams request, CancellationToken ct)
    {
        var uri = request.TextDocument.Uri;
        var text = _documents.Get(uri.ToString());
        if (text is null) return new CompletionList();

        var projectDir = Path.GetDirectoryName(uri.GetFileSystemPath()) ?? ".";

        // Directives sit on lines of their own, so the two cases cannot overlap; the order is
        // a matter of reading, not of correctness
        var directive = DirectiveContextScanner.Detect(
            text, request.Position.Line, request.Position.Character);
        if (directive.Name is not null)
        {
            var registry = (await _configuration.GetAsync(projectDir, ct)).Registry;
            var root = ProjectRoot.Find(projectDir);

            // What the client should replace, said outright rather than left to it. A path
            // holds a slash, and the editor's own idea of the word under the caret stops at
            // one - completing over `Views` then produced `ViewsViews/SiteMaster.dotmaster`.
            var replaced = new Range(
                new Position(request.Position.Line,
                             request.Position.Character - directive.WrittenValue.Length),
                request.Position);

            return new CompletionList(
                DirectiveCompletion
                    .Suggest(registry, directive,
                             root is null ? null : extension => ViewFiles.Find(root, extension))
                    .Select(suggestion => ToCompletionItem(suggestion, replaced)));
        }

        var context = CompletionContextScanner.Detect(
            text, request.Position.Line, request.Position.Character);
        if (context.Target == CompletionTarget.None) return new CompletionList();

        var configuration = await _configuration.GetAsync(projectDir, ct);

        var suggestions = ControlCompletion.Suggest(configuration.Registry, context);

        return new CompletionList(suggestions.Select(ToCompletionItem));
    }

    /// <summary>
    /// A directive's value is inserted as plain text: it is a type name or a path, with nothing
    /// for a snippet placeholder to do.
    /// </summary>
    private static CompletionItem ToCompletionItem(
        DirectiveSuggestion suggestion, Range replaced) =>
        new()
        {
            Label = suggestion.Label,
            Kind = CompletionItemKind.Reference,
            Detail = suggestion.Detail,
            SortText = suggestion.SortText,
            TextEdit = new TextEditOrInsertReplaceEdit(
                new TextEdit { Range = replaced, NewText = suggestion.Label }),
            InsertTextFormat = InsertTextFormat.PlainText,
        };

    private CompletionItem ToCompletionItem(CompletionSuggestion suggestion) =>
        new()
        {
            Label = suggestion.Label,
            Kind = suggestion.Kind switch
            {
                SuggestionKind.Prefix => CompletionItemKind.Module,
                SuggestionKind.Tag => CompletionItemKind.Class,
                _ => CompletionItemKind.Property,
            },
            Detail = suggestion.Detail,
            SortText = suggestion.SortText,
            InsertText = suggestion.IsSnippet && !_snippets
                ? suggestion.InsertText.Replace("$0", "")
                : suggestion.InsertText,
            InsertTextFormat = suggestion.IsSnippet && _snippets
                ? InsertTextFormat.Snippet
                : InsertTextFormat.PlainText,
        };
}
