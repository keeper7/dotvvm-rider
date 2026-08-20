using System.Collections.Concurrent;

namespace DotVVM.LanguageServer.Documents;

/// <summary>Obsah otevřených dokumentů, klíčovaný podle URI.</summary>
public sealed class DocumentStore
{
    private readonly ConcurrentDictionary<string, string> _documents = new(StringComparer.Ordinal);

    public void Set(string uri, string text) => _documents[uri] = text;

    public string? Get(string uri) => _documents.TryGetValue(uri, out var text) ? text : null;

    public void Remove(string uri) => _documents.TryRemove(uri, out _);

    public IReadOnlyCollection<string> OpenUris => _documents.Keys.ToList();
}
