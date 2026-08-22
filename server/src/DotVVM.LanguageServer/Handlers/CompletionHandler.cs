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

    public CompletionRegistrationOptions GetRegistrationOptions(
        CompletionCapability capability, ClientCapabilities clientCapabilities) =>
        new()
        {
            DocumentSelector = new TextDocumentSelector(
                new TextDocumentFilter { Pattern = "**/*.dothtml" },
                new TextDocumentFilter { Pattern = "**/*.dotmaster" },
                new TextDocumentFilter { Pattern = "**/*.dotcontrol" }),
            TriggerCharacters = new Container<string>("<", ":")
        };

    public async Task<CompletionList> Handle(CompletionParams request, CancellationToken ct)
    {
        var uri = request.TextDocument.Uri;
        var text = _documents.Get(uri.ToString());
        if (text is null) return new CompletionList();

        var projectDir = Path.GetDirectoryName(uri.GetFileSystemPath()) ?? ".";
        var configuration = await _configuration.GetAsync(projectDir, ct);
        var registry = configuration.Registry;

        var prefix = FindPrefixBeforeCursor(text, request.Position);

        // After "<prefix:" offer that prefix's controls, otherwise offer the prefixes themselves
        if (prefix is not null && registry.IsKnownPrefix(prefix))
        {
            return new CompletionList(registry.GetTagsForPrefix(prefix).Select(tag =>
                new CompletionItem
                {
                    Label = tag,
                    Kind = CompletionItemKind.Class,
                    Detail = $"{prefix}:{tag}"
                }));
        }

        return new CompletionList(registry.AllPrefixes.Select(p =>
            new CompletionItem
            {
                Label = p,
                Kind = CompletionItemKind.Module,
                InsertText = p + ":",
                Detail = "DotVVM tag prefix"
            }));
    }

    /// <summary>Returns the prefix when the caret sits after "&lt;prefix:".</summary>
    private static string? FindPrefixBeforeCursor(string text, Position position)
    {
        var lines = text.Split('\n');
        if (position.Line >= lines.Length) return null;

        var line = lines[position.Line];
        var upto = line[..Math.Min(position.Character, line.Length)];

        var lt = upto.LastIndexOf('<');
        if (lt < 0) return null;

        var afterLt = upto[(lt + 1)..];
        var colon = afterLt.IndexOf(':');
        if (colon < 0) return null;

        var candidate = afterLt[..colon];
        return candidate.All(c => char.IsLetterOrDigit(c) || c == '_') && candidate.Length > 0
            ? candidate
            : null;
    }
}
