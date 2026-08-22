using DotVVM.LanguageServer.Analysis;
using DotVVM.LanguageServer.Model;

namespace DotVVM.LanguageServer.Configuration;

/// <summary>
/// Connects a markup control to the class that holds its properties.
///
/// A markup control is registered by file, not by type, so the namespace lookup never reaches it.
/// Its properties live in the code-behind class named by @baseType inside that file; this reads
/// the file and records the name, leaving the lookup itself to the registry.
///
/// The file reader is a parameter so the resolution can be tested without touching a disk.
/// </summary>
public static class MarkupControlResolver
{
    public static ControlRegistry Resolve(
        ControlRegistry registry, string projectRoot, Func<string, string?> readFile)
    {
        var registrations = registry.Registrations.Select(r =>
        {
            if (!r.IsMarkupControl || r.Src is null || r.BaseTypeName is not null) return r;

            var text = readFile(Path.Combine(projectRoot, r.Src));
            if (text is null) return r;

            var baseType = BaseTypeDirective.Parse(text);
            return baseType is null ? r : r with { BaseTypeName = baseType };
        });

        return new ControlRegistry(registrations, registry.Controls);
    }

    public static string? ReadFile(string path) =>
        File.Exists(path) ? File.ReadAllText(path) : null;
}
