namespace DotVVM.LanguageServer.Model;

/// <summary>
/// Queries over the registered controls. Free of both LSP and DotVVM dependencies: data and
/// lookups, nothing more.
/// </summary>
public sealed class ControlRegistry
{
    private readonly IReadOnlyList<ControlRegistration> _registrations;
    private readonly IReadOnlyList<ControlInfo> _controls;

    public ControlRegistry(
        IEnumerable<ControlRegistration> registrations,
        IEnumerable<ControlInfo> controls)
    {
        _registrations = registrations.ToList();
        _controls = controls.ToList();
    }

    public static ControlRegistry Empty { get; } =
        new(Array.Empty<ControlRegistration>(), Array.Empty<ControlInfo>());

    public IReadOnlyList<ControlRegistration> Registrations => _registrations;

    public IReadOnlyList<ControlInfo> Controls => _controls;

    public IReadOnlyCollection<string> AllPrefixes =>
        _registrations.Select(r => r.TagPrefix).Distinct().ToList();

    public bool IsKnownPrefix(string prefix) =>
        _registrations.Any(r => string.Equals(r.TagPrefix, prefix, StringComparison.Ordinal));

    public bool IsKnownTag(string prefix, string tagName)
    {
        if (_registrations.Any(r => r.TagPrefix == prefix
                                    && r.IsMarkupControl
                                    && r.TagName == tagName))
        {
            return true;
        }

        return NamespacesFor(prefix)
            .Any(ns => _controls.Any(c => c.Namespace == ns && c.TagName == tagName));
    }

    public IReadOnlyCollection<string> GetTagsForPrefix(string prefix)
    {
        var markup = _registrations
            .Where(r => r.TagPrefix == prefix && r.IsMarkupControl)
            .Select(r => r.TagName!);

        var namespaces = NamespacesFor(prefix).ToHashSet(StringComparer.Ordinal);
        var typed = _controls
            .Where(c => namespaces.Contains(c.Namespace))
            .Select(c => c.TagName);

        return markup.Concat(typed).Distinct().ToList();
    }

    public ControlInfo? GetControl(string prefix, string tagName)
    {
        // A markup control is registered by file, so the namespace lookup below never finds it.
        // Its properties belong to the class named by @baseType, resolved by MarkupControlResolver.
        var markup = _registrations.FirstOrDefault(r =>
            r.TagPrefix == prefix && r.IsMarkupControl && r.TagName == tagName
            && r.BaseTypeName is not null);

        if (markup is not null)
        {
            var byType = _controls.FirstOrDefault(c => c.FullTypeName == markup.BaseTypeName);
            if (byType is not null) return byType;
        }

        var namespaces = NamespacesFor(prefix).ToHashSet(StringComparer.Ordinal);
        return _controls.FirstOrDefault(c => namespaces.Contains(c.Namespace) && c.TagName == tagName);
    }

    private IEnumerable<string> NamespacesFor(string prefix) =>
        _registrations
            .Where(r => r.TagPrefix == prefix && r.Namespace is not null)
            .Select(r => r.Namespace!);

    /// <summary>Merges two registries; values from <paramref name="other"/> win.</summary>
    public ControlRegistry MergedWith(ControlRegistry other) =>
        new(_registrations.Concat(other._registrations).Distinct(),
            other._controls.Concat(_controls)
                 .GroupBy(c => c.FullTypeName, StringComparer.Ordinal)
                 .Select(g => g.First()));
}
