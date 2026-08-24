using DotVVM.LanguageServer.Analysis;
using DotVVM.LanguageServer.Compilation;
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
    private readonly LiveValidation _live;

    public CompletionHandler(
        DocumentStore documents, ProjectConfigurationProvider configuration, LiveValidation live)
    {
        _documents = documents;
        _configuration = configuration;
        _live = live;
    }

    /// <summary>
    /// Whether the client can handle a $0 placeholder. Captured while registering, because
    /// Handle does not see the capabilities and inserting "$0" literally would be worse than
    /// inserting nothing.
    /// </summary>
    private bool _snippets;

    private static readonly System.Text.RegularExpressions.Regex Placeholder =
        new(@"\$\d+", System.Text.RegularExpressions.RegexOptions.Compiled);

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
            // The space is what opens the attribute list without the user asking for it, and
            // inside a binding it is what follows the kind's colon. The brace opens a binding
            // and the dot walks into a member, so both belong here as well.
            TriggerCharacters = new Container<string>("<", ":", " ", "{", ".")
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

        var binding = BindingContextScanner.Detect(
            text, request.Position.Line, request.Position.Character);
        if (binding.Target != BindingTarget.None)
        {
            return await CompleteBindingAsync(request, text, binding, ct);
        }

        var context = CompletionContextScanner.Detect(
            text, request.Position.Line, request.Position.Character);
        if (context.Target == CompletionTarget.None) return new CompletionList();

        var configuration = await _configuration.GetAsync(projectDir, ct);

        var suggestions = ControlCompletion.Suggest(configuration.Registry, context);

        return new CompletionList(suggestions.Select(ToCompletionItem));
    }

    /// <summary>
    /// Inside a binding two different things may be asked for: which kind of binding this is,
    /// which the server knows on its own, and which member may be written, which only the
    /// compiler process can answer - it alone holds the project's types and can say what the
    /// data context is at that place in the file.
    /// </summary>
    private async Task<CompletionList> CompleteBindingAsync(
        CompletionParams request, string text, BindingContext binding, CancellationToken ct)
    {
        var filePath = request.TextDocument.Uri.GetFileSystemPath();

        // Said outright rather than left to the editor, the same reason as for a directive's
        // value: what is replaced is the word being typed and nothing around it.
        var replaced = new Range(
            new Position(request.Position.Line,
                         request.Position.Character - binding.Word.Length),
            request.Position);

        if (binding.Target == BindingTarget.BindingKind)
        {
            return new CompletionList(
                BindingCompletion.Kinds(filePath).Select(s => ToCompletionItem(s, replaced)));
        }

        var offset = TextPosition.OffsetOf(
            text, request.Position.Line, request.Position.Character);

        var members = await _live.CompleteAsync(
            Path.GetDirectoryName(filePath) ?? ".", filePath, text, offset, binding.Path,
            binding.Kind ?? "value", ct);

        // No answer at all - no build to run against, or the compiler switched off. Offering the
        // data context's members guessed from the text would be worse than offering nothing.
        return members is null
            ? new CompletionList()
            : new CompletionList(members.Select(m => ToCompletionItem(m, replaced)));
    }

    /// <summary>
    /// A binding kind carries its own colon, so that typing the space after it opens the list of
    /// members - the space being a trigger character.
    /// </summary>
    private static CompletionItem ToCompletionItem(CompletionSuggestion suggestion, Range replaced) =>
        new()
        {
            Label = suggestion.Label,
            Kind = CompletionItemKind.Keyword,
            Detail = suggestion.Detail,
            SortText = suggestion.SortText,
            TextEdit = new TextEditOrInsertReplaceEdit(
                new TextEdit { Range = replaced, NewText = suggestion.InsertText }),
            InsertTextFormat = InsertTextFormat.PlainText,
        };

    /// <summary>
    /// Properties sort in front of methods, those in front of the classes an import brings in,
    /// and all of them in front of the binding's own words - which begin with an underscore and
    /// would otherwise head an alphabetic list.
    /// </summary>
    private CompletionItem ToCompletionItem(CompilerCompletionItem member, Range replaced) =>
        new()
        {
            Label = member.Label,
            Kind = member.Kind switch
            {
                "method" => CompletionItemKind.Method,
                "class" => CompletionItemKind.Class,
                "parameter" => CompletionItemKind.Variable,
                _ => CompletionItemKind.Property,
            },
            Detail = member.Detail,
            SortText = member.Kind switch
            {
                "method" => "1" + member.Label,
                "class" => "2" + member.Label,
                "parameter" => "3" + member.Label,
                _ => "0" + member.Label,
            },
            TextEdit = new TextEditOrInsertReplaceEdit(new TextEdit
            {
                Range = replaced,
                NewText = member.Snippet && !_snippets
                    ? Placeholder.Replace(member.InsertText, "")
                    : member.InsertText,
            }),
            InsertTextFormat = member.Snippet && _snippets
                ? InsertTextFormat.Snippet
                : InsertTextFormat.PlainText,
        };

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
            // A client without snippet support would take the placeholders literally. A group
            // uses two of them, so stripping "$0" alone is not enough.
            InsertText = suggestion.IsSnippet && !_snippets
                ? Placeholder.Replace(suggestion.InsertText, "")
                : suggestion.InsertText,
            InsertTextFormat = suggestion.IsSnippet && _snippets
                ? InsertTextFormat.Snippet
                : InsertTextFormat.PlainText,
        };
}
