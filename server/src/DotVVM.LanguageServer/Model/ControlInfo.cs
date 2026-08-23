namespace DotVVM.LanguageServer.Model;

/// <summary>A control known by its type, including its properties.</summary>
public record ControlInfo(
    string FullTypeName,
    string? BaseType,
    string? DefaultContentProperty,
    IReadOnlyList<ControlProperty> Properties,
    IReadOnlyList<ControlPropertyGroup>? PropertyGroups = null)
{
    /// <summary>
    /// The property families the control accepts — Class-, Style-, Param- and their kind. Kept
    /// apart from Properties because a group has no name of its own: the author writes one.
    /// </summary>
    public IReadOnlyList<ControlPropertyGroup> Groups =>
        PropertyGroups ?? Array.Empty<ControlPropertyGroup>();

    /// <summary>
    /// The base type without the assembly. The serialized configuration writes it as
    /// "Type, Assembly" while FullTypeName carries no assembly, so the two would never match.
    /// </summary>
    public string? BareBaseType
    {
        get
        {
            var name = BaseType?.Split(',')[0].Trim();
            return string.IsNullOrEmpty(name) ? null : name;
        }
    }

    /// <summary>The tag name, that is the last segment of the full type name.</summary>
    public string TagName => FullTypeName[(FullTypeName.LastIndexOf('.') + 1)..];

    /// <summary>The type's namespace, that is everything before the last segment.</summary>
    public string Namespace
    {
        get
        {
            var idx = FullTypeName.LastIndexOf('.');
            return idx < 0 ? string.Empty : FullTypeName[..idx];
        }
    }
}
