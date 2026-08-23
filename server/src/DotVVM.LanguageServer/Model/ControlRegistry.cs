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

    /// <summary>
    /// The property families a plain HTML element accepts. An element written in a view compiles
    /// to HtmlGenericControl, so it takes whatever that control declares: `Class-required` on a
    /// &lt;label&gt; is ordinary DotVVM and appears in real projects, though the offer used to
    /// stop at prefixed tags. Verified against the framework's own resolver, which accepts it.
    ///
    /// Empty before a project has been built: tier 1 lists the controls a view writes by name
    /// and HtmlGenericControl is not one of them.
    /// </summary>
    public IReadOnlyList<ControlPropertyGroup> HtmlElementGroups =>
        _controls.FirstOrDefault(c => c.FullTypeName == HtmlGenericControlType)?.Groups
        ?? Array.Empty<ControlPropertyGroup>();

    private const string HtmlGenericControlType = "DotVVM.Framework.Controls.HtmlGenericControl";

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

    /// <summary>
    /// The control behind a tag, with everything it inherits already collected. What
    /// <see cref="Controls"/> holds is what each source declared; this is what the tag can
    /// actually be written with.
    /// </summary>
    public ControlInfo? GetControl(string prefix, string tagName)
    {
        var control = FindControl(prefix, tagName);
        return control is null ? null : Inherited(control);
    }

    private ControlInfo? FindControl(string prefix, string tagName)
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

    /// <summary>
    /// Walks the base type chain and collects what the control inherits. The assembly probe
    /// resolves that itself, but the serialized configuration lists only what each type
    /// declares: dot:Label holds a single property there — For — while Text, Visible and the
    /// whole Class- group sit on HtmlGenericControl above it. The nearest declaration wins,
    /// which is the one that overrides.
    /// </summary>
    private ControlInfo Inherited(ControlInfo control)
    {
        var properties = new List<ControlProperty>();
        var groups = new List<ControlPropertyGroup>();
        var seenProperties = new HashSet<string>(StringComparer.Ordinal);
        var seenGroups = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);

        for (var current = control; current is not null; current = BaseOf(current))
        {
            // No real type chain loops, but the registry is merged from three sources and one
            // wrong entry must not spin here for ever
            if (!visited.Add(current.FullTypeName)) break;

            foreach (var property in current.Properties)
            {
                if (seenProperties.Add(property.Name)) properties.Add(property);
            }

            foreach (var group in current.Groups)
            {
                if (seenGroups.Add(group.Prefix)) groups.Add(group);
            }
        }

        return control with { Properties = properties, PropertyGroups = groups };
    }

    private ControlInfo? BaseOf(ControlInfo control) =>
        control.BareBaseType is { } name
            ? _controls.FirstOrDefault(c => c.FullTypeName == name)
            : null;

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
