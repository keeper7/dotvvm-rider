using DotVVM.LanguageServer.Analysis;
using DotVVM.LanguageServer.Configuration;
using DotVVM.LanguageServer.Documents;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace DotVVM.LanguageServer.Handlers;

public class DocumentSyncHandler : TextDocumentSyncHandlerBase
{
    private readonly ILanguageServerFacade _server;
    private readonly DocumentStore _documents;
    private readonly ProjectConfigurationProvider _configuration;

    public DocumentSyncHandler(
        ILanguageServerFacade server,
        DocumentStore documents,
        ProjectConfigurationProvider configuration)
    {
        _server = server;
        _documents = documents;
        _configuration = configuration;
    }

    private static readonly TextDocumentSelector Selector = new(
        new TextDocumentFilter { Pattern = "**/*.dothtml" },
        new TextDocumentFilter { Pattern = "**/*.dotmaster" },
        new TextDocumentFilter { Pattern = "**/*.dotcontrol" });

    public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri) =>
        new(uri, "dotvvm");

    protected override TextDocumentSyncRegistrationOptions CreateRegistrationOptions(
        TextSynchronizationCapability capability, ClientCapabilities clientCapabilities) =>
        new()
        {
            DocumentSelector = Selector,
            Change = TextDocumentSyncKind.Full
        };

    public override async Task<Unit> Handle(DidOpenTextDocumentParams request, CancellationToken ct)
    {
        _documents.Set(request.TextDocument.Uri.ToString(), request.TextDocument.Text);
        await PublishDiagnosticsAsync(request.TextDocument.Uri, request.TextDocument.Text, ct);
        return Unit.Value;
    }

    public override async Task<Unit> Handle(DidChangeTextDocumentParams request, CancellationToken ct)
    {
        // Registrujeme Full sync, takže poslední change nese celý obsah
        var text = request.ContentChanges.LastOrDefault()?.Text ?? string.Empty;
        _documents.Set(request.TextDocument.Uri.ToString(), text);
        await PublishDiagnosticsAsync(request.TextDocument.Uri, text, ct);
        return Unit.Value;
    }

    public override Task<Unit> Handle(DidCloseTextDocumentParams request, CancellationToken ct)
    {
        _documents.Remove(request.TextDocument.Uri.ToString());
        return Unit.Task;
    }

    public override Task<Unit> Handle(DidSaveTextDocumentParams request, CancellationToken ct) =>
        Unit.Task;

    private async Task PublishDiagnosticsAsync(DocumentUri uri, string text, CancellationToken ct)
    {
        var projectDir = Path.GetDirectoryName(uri.GetFileSystemPath()) ?? ".";
        var configuration = await _configuration.GetAsync(projectDir, ct);
        var issues = TagValidator.Validate(text, configuration.Registry);

        // Klient podle stupně kreslí status bar. Bez toho by uživatel neměl jak zjistit,
        // proč server nezná jeho vlastní kontrolky — prázdný registr se navenek neprojeví.
        _server.SendNotification("dotvvm/configurationTier", new { tier = configuration.SourceName });

        _server.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
        {
            Uri = uri,
            Diagnostics = new Container<Diagnostic>(issues.Select(ToDiagnostic))
        });
    }

    private static Diagnostic ToDiagnostic(ValidationIssue issue) => new()
    {
        Message = issue.Message,
        Severity = issue.Level switch
        {
            DiagnosticLevel.Error => DiagnosticSeverity.Error,
            DiagnosticLevel.Warning => DiagnosticSeverity.Warning,
            _ => DiagnosticSeverity.Information
        },
        Source = "dotvvm",
        Range = new Range(
            new Position(issue.Line, issue.Character),
            new Position(issue.Line, issue.Character + issue.Length))
    };
}
