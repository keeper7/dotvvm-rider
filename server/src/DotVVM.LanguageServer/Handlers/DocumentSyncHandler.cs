using DotVVM.LanguageServer.Analysis;
using DotVVM.LanguageServer.Compilation;
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
    private readonly LiveValidation _live;

    public DocumentSyncHandler(
        ILanguageServerFacade server,
        DocumentStore documents,
        ProjectConfigurationProvider configuration,
        LiveValidation live)
    {
        _server = server;
        _documents = documents;
        _configuration = configuration;
        _live = live;
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
        ScheduleCompilation(request.TextDocument.Uri, request.TextDocument.Text);
        return Unit.Value;
    }

    public override async Task<Unit> Handle(DidChangeTextDocumentParams request, CancellationToken ct)
    {
        // We register Full sync, so the last change carries the whole content
        var text = request.ContentChanges.LastOrDefault()?.Text ?? string.Empty;
        _documents.Set(request.TextDocument.Uri.ToString(), text);
        await PublishDiagnosticsAsync(request.TextDocument.Uri, text, ct);
        ScheduleCompilation(request.TextDocument.Uri, text);
        return Unit.Value;
    }

    public override async Task<Unit> Handle(DidCloseTextDocumentParams request, CancellationToken ct)
    {
        _documents.Remove(request.TextDocument.Uri.ToString());
        await _live.ForgetAsync(request.TextDocument.Uri.ToString());
        return Unit.Value;
    }

    /// <summary>
    /// Saving is the author saying the file is finished, so the compiler runs at once rather
    /// than after the debounce a change gets.
    /// </summary>
    public override async Task<Unit> Handle(DidSaveTextDocumentParams request, CancellationToken ct)
    {
        var uri = request.TextDocument.Uri;
        var text = _documents.Get(uri.ToString());
        if (text is null) return Unit.Value;

        var filePath = uri.GetFileSystemPath();
        var projectDir = Path.GetDirectoryName(filePath) ?? ".";
        var compiled = await _live.CompileAsync(projectDir, filePath, text, ct);
        if (compiled is not null) await PublishAsync(uri, text, compiled, ct);

        return Unit.Value;
    }

    private async Task PublishDiagnosticsAsync(DocumentUri uri, string text, CancellationToken ct)
    {
        var filePath = uri.GetFileSystemPath();
        var projectDir = Path.GetDirectoryName(filePath) ?? ".";
        var configuration = await _configuration.GetAsync(projectDir, ct);

        var issues = TagValidator
            .Validate(text, configuration.Registry, configuration.KnowsProjectPrefixes)
            .Concat(DirectiveValidator.Validate(
                text, filePath, configuration.Registry, MasterPageExists(projectDir)))
            .ToList();

        // The client paints the status bar from this. Without it the user would have no way to
        // tell why the server does not know their own controls: an empty registry is invisible.
        _server.SendNotification("dotvvm/configurationTier", new { tier = configuration.SourceName });

        // Navigation from a tag has to happen in the plugin: the platform asks an LSP server
        // only where an element carries no reference of its own, and an XmlTag carries one.
        // The registrations are what the plugin cannot work out for itself, so they travel.
        _server.SendNotification("dotvvm/controlRegistrations", new
        {
            registrations = configuration.Registry.Registrations.Select(r => new
            {
                prefix = r.TagPrefix,
                tagName = r.TagName,
                src = r.Src,
                @namespace = r.Namespace,
                assembly = r.Assembly,
            })
        });

        Publish(uri, issues, Array.Empty<CompilerIssue>());
    }

    /// <summary>
    /// Publishes the structural findings together with what the view compiler said. Both are
    /// recomputed rather than remembered: the structural pass costs nothing, and one list has to
    /// go out at a time - a second publish would erase the first.
    /// </summary>
    private async Task PublishAsync(
        DocumentUri uri, string text, IReadOnlyList<CompilerDiagnostic> compiled,
        CancellationToken ct)
    {
        var filePath = uri.GetFileSystemPath();
        var projectDir = Path.GetDirectoryName(filePath) ?? ".";
        var configuration = await _configuration.GetAsync(projectDir, ct);

        var issues = TagValidator
            .Validate(text, configuration.Registry, configuration.KnowsProjectPrefixes)
            .Concat(DirectiveValidator.Validate(
                text, filePath, configuration.Registry, MasterPageExists(projectDir)))
            .ToList();

        Publish(uri, issues, DiagnosticConversion.ToIssues(compiled));
    }

    private void Publish(
        DocumentUri uri,
        IReadOnlyList<ValidationIssue> structural,
        IReadOnlyList<CompilerIssue> compiled) =>
        _server.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
        {
            Uri = uri,
            Diagnostics = new Container<Diagnostic>(
                structural.Select(ToDiagnostic).Concat(compiled.Select(ToDiagnostic)))
        });

    /// <summary>
    /// Asks for a compilation once the typing stops. The result arrives on its own thread, so it
    /// publishes the whole set again rather than adding to what is already shown.
    /// </summary>
    private void ScheduleCompilation(DocumentUri uri, string text)
    {
        var filePath = uri.GetFileSystemPath();
        var projectDir = Path.GetDirectoryName(filePath) ?? ".";

        _live.Schedule(uri.ToString(), projectDir, filePath, text,
            compiled => PublishAsync(uri, text, compiled, CancellationToken.None));
    }

    /// <summary>
    /// Resolves a master page path against the project root, or null when there is no root to
    /// resolve it against - and then the path goes unjudged rather than being guessed at.
    /// </summary>
    private static Func<string, bool>? MasterPageExists(string projectDir)
    {
        var root = ProjectRoot.Find(projectDir);
        if (root is null) return null;

        return path => File.Exists(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static Diagnostic ToDiagnostic(CompilerIssue issue) => new()
    {
        Message = issue.Message,
        Severity = issue.Level switch
        {
            DiagnosticLevel.Error => DiagnosticSeverity.Error,
            DiagnosticLevel.Warning => DiagnosticSeverity.Warning,
            _ => DiagnosticSeverity.Information
        },
        // Named apart from the structural findings, so a reader can tell which pass said what
        Source = "dotvvm-compiler",
        Range = new Range(
            new Position(issue.Line, issue.Character),
            new Position(issue.EndLine, issue.EndCharacter))
    };

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
