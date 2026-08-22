using DotVVM.LanguageServer.Analysis;
using DotVVM.LanguageServer.Configuration;
using DotVVM.LanguageServer.Documents;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

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
        _snippets = capability.CompletionItem?.SnippetSupport ?? false;

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

        var context = CompletionContextScanner.Detect(
            text, request.Position.Line, request.Position.Character);
        if (context.Target == CompletionTarget.None) return new CompletionList();

        var projectDir = Path.GetDirectoryName(uri.GetFileSystemPath()) ?? ".";
        var configuration = await _configuration.GetAsync(projectDir, ct);

        var suggestions = ControlCompletion.Suggest(configuration.Registry, context);

        return new CompletionList(suggestions.Select(ToCompletionItem));
    }

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
