using DotVVM.LanguageServer.Analysis;
using DotVVM.LanguageServer.Documents;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace DotVVM.LanguageServer.Handlers;

public class DefinitionHandler : IDefinitionHandler
{
    private readonly DocumentStore _documents;

    public DefinitionHandler(DocumentStore documents) => _documents = documents;

    public DefinitionRegistrationOptions GetRegistrationOptions(
        DefinitionCapability capability, ClientCapabilities clientCapabilities) =>
        new()
        {
            DocumentSelector = new TextDocumentSelector(
                new TextDocumentFilter { Pattern = "**/*.dothtml" },
                new TextDocumentFilter { Pattern = "**/*.dotmaster" },
                new TextDocumentFilter { Pattern = "**/*.dotcontrol" })
        };

    public Task<LocationOrLocationLinks?> Handle(DefinitionParams request, CancellationToken ct)
    {
        var text = _documents.Get(request.TextDocument.Uri.ToString());
        if (text is null) return Task.FromResult<LocationOrLocationLinks?>(null);

        var reference = ViewModelDirective.Parse(text);
        if (reference is null || reference.Line != request.Position.Line)
        {
            return Task.FromResult<LocationOrLocationLinks?>(null);
        }

        var projectDir = Path.GetDirectoryName(request.TextDocument.Uri.GetFileSystemPath());
        var file = FindViewModelFile(projectDir, reference.TypeName);
        if (file is null) return Task.FromResult<LocationOrLocationLinks?>(null);

        var location = new Location
        {
            Uri = DocumentUri.FromFileSystemPath(file),
            Range = new Range(new Position(0, 0), new Position(0, 0))
        };

        return Task.FromResult<LocationOrLocationLinks?>(
            new LocationOrLocationLinks(location));
    }

    /// <summary>
    /// Finds the file declaring the type. It searches by the last segment of the type name,
    /// because the file is often named differently from the view.
    /// </summary>
    private static string? FindViewModelFile(string? startDir, string typeName)
    {
        if (startDir is null) return null;

        var bare = typeName.Split('<')[0];
        var shortName = bare[(bare.LastIndexOf('.') + 1)..];

        var root = FindProjectRoot(startDir) ?? startDir;

        foreach (var file in Directory.EnumerateFiles(root, shortName + ".cs",
                                                      SearchOption.AllDirectories))
        {
            return file;
        }

        // The file may have a different name, so search the contents
        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            {
                continue;
            }

            var content = File.ReadAllText(file);
            if (content.Contains($"class {shortName}", StringComparison.Ordinal)) return file;
        }

        return null;
    }

    private static string? FindProjectRoot(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir is not null)
        {
            if (dir.EnumerateFiles("*.csproj").Any()) return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }
}
