using DotVVM.LanguageServer.Analysis;
using DotVVM.LanguageServer.Configuration;
using DotVVM.LanguageServer.Documents;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace DotVVM.LanguageServer.Handlers;

public class HoverHandler : IHoverHandler
{
    private readonly DocumentStore _documents;
    private readonly ProjectConfigurationProvider _configuration;

    public HoverHandler(DocumentStore documents, ProjectConfigurationProvider configuration)
    {
        _documents = documents;
        _configuration = configuration;
    }

    public HoverRegistrationOptions GetRegistrationOptions(
        HoverCapability capability, ClientCapabilities clientCapabilities) =>
        new()
        {
            DocumentSelector = new TextDocumentSelector(
                new TextDocumentFilter { Pattern = "**/*.dothtml" },
                new TextDocumentFilter { Pattern = "**/*.dotmaster" },
                new TextDocumentFilter { Pattern = "**/*.dotcontrol" })
        };

    public async Task<Hover?> Handle(HoverParams request, CancellationToken ct)
    {
        var uri = request.TextDocument.Uri;
        var text = _documents.Get(uri.ToString());
        if (text is null) return null;

        // Find the tag the caret is on
        var tag = DothtmlScanner.ScanTags(text).FirstOrDefault(t =>
            t.Line == request.Position.Line &&
            request.Position.Character >= t.Character &&
            request.Position.Character <= t.Character + t.Length);

        if (tag is null) return null;

        var projectDir = Path.GetDirectoryName(uri.GetFileSystemPath()) ?? ".";
        var configuration = await _configuration.GetAsync(projectDir, ct);
        var control = configuration.Registry.GetControl(tag.Prefix, tag.TagName);

        var lines = new List<string> { $"**{tag.Prefix}:{tag.TagName}**" };

        if (control is not null)
        {
            lines.Add("");
            lines.Add($"`{control.FullTypeName}`");

            if (control.DefaultContentProperty is not null)
            {
                lines.Add("");
                lines.Add($"Default content property: `{control.DefaultContentProperty}`");
            }

            if (control.Properties.Count > 0)
            {
                lines.Add("");
                lines.Add("Vlastnosti: " + string.Join(", ", control.Properties.Take(15)
                    .Select(p => $"`{p}`")));
            }
        }
        else
        {
            lines.Add("");
            lines.Add("_Markup control \u2014 properties are not known._");
        }

        return new Hover
        {
            Contents = new MarkedStringsOrMarkupContent(new MarkupContent
            {
                Kind = MarkupKind.Markdown,
                Value = string.Join("\n", lines)
            }),
            Range = new Range(
                new Position(tag.Line, tag.Character),
                new Position(tag.Line, tag.Character + tag.Length))
        };
    }
}
