namespace DotVVM.LanguageServer.Model;

/// <summary>A control known by its type, including its properties.</summary>
public record ControlInfo(
    string FullTypeName,
    string? BaseType,
    string? DefaultContentProperty,
    IReadOnlyList<ControlProperty> Properties)
{
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
