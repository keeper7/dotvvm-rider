namespace DotVVM.LanguageServer.Model;

/// <summary>
/// Queries over the registered controls. Free of both LSP and DotVVM dependencies: data and
/// lookups, nothing more.
/// </summary>
public sealed class ControlRegistry
{
    private readonly IReadOnlyList<ControlRegistration> _registrations;
    private readonly IReadOnlyList<ControlInfo> _controls;
    private readonly IReadOnlyList<ControlProperty> _attached;

    public ControlRegistry(
        IEnumerable<ControlRegistration> registrations,
        IEnumerable<ControlInfo> controls,
        IEnumerable<ControlProperty>? attachedProperties = null,
        ProjectTypes? types = null)
    {
        _registrations = registrations.ToList();
        _controls = controls.ToList();
        _attached = attachedProperties?.ToList() ?? new List<ControlProperty>();
        Types = types ?? ProjectTypes.Empty;
    }

    public static ControlRegistry Empty { get; } =
        new(Array.Empty<ControlRegistration>(), Array.Empty<ControlInfo>());

    public IReadOnlyList<ControlRegistration> Registrations => _registrations;

    public IReadOnlyList<ControlInfo> Controls => _controls;

    /// <summary>
    /// Properties written as Owner.Name on any element — Validation.Enabled and its kind. They
    /// belong to no control, so they cannot live in ControlInfo.
    /// </summary>
    public IReadOnlyList<ControlProperty> AttachedProperties => _attached;

    /// <summary>
    /// What a directive's value can name: the project's view models and namespaces. Only the
    /// assembly probe fills this, so on the lower tiers it stays empty and the directive
    /// completion says nothing — the same rule the validator follows.
    /// </summary>
    public ProjectTypes Types { get; }

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

    /// <summary>Whether the tag is registered as a markup control, that is by file.</summary>
    public bool IsMarkupControl(string prefix, string tagName) =>
        MarkupRegistration(prefix, tagName) is not null;

    public ControlInfo? GetControl(string prefix, string tagName)
    {
        // A markup control is registered by file, so the namespace lookup below never finds it.
        // Its properties belong to the class named by @baseType, resolved by MarkupControlResolver.
        var baseTypeName = MarkupRegistration(prefix, tagName)?.BaseTypeName;
        if (baseTypeName is not null)
        {
            var byType = _controls.FirstOrDefault(c => c.FullTypeName == baseTypeName);
            if (byType is not null) return byType;
        }

        var namespaces = NamespacesFor(prefix).ToHashSet(StringComparer.Ordinal);
        return _controls.FirstOrDefault(c => namespaces.Contains(c.Namespace) && c.TagName == tagName);
    }

    private ControlRegistration? MarkupRegistration(string prefix, string tagName) =>
        _registrations.FirstOrDefault(r =>
            r.TagPrefix == prefix && r.IsMarkupControl && r.TagName == tagName);

    private IEnumerable<string> NamespacesFor(string prefix) =>
        _registrations
            .Where(r => r.TagPrefix == prefix && r.Namespace is not null)
            .Select(r => r.Namespace!);

    /// <summary>Merges two registries; values from <paramref name="other"/> win.</summary>
    public ControlRegistry MergedWith(ControlRegistry other) =>
        new(_registrations.Concat(other._registrations).Distinct(),
            other._controls.Concat(_controls)
                 .GroupBy(c => c.FullTypeName, StringComparer.Ordinal)
                 .Select(g => g.First()),
            other._attached.Concat(_attached)
                 .GroupBy(p => p.Name, StringComparer.Ordinal)
                 .Select(g => g.First()),
            Types.MergedWith(other.Types));
}
