namespace DotVVM.LanguageServer.Model;

/// <summary>
/// A family of properties written as one prefix followed by a free name — Class-active,
/// Style-width, Param-Id. What follows the prefix is the author's own word, so nothing can
/// offer it; the prefix is all completion has to say.
///
/// A group whose prefix is empty means "any attribute at all goes here" — HtmlGenericControl
/// declares one. There is nothing to offer for it, so such prefixes are dropped when read.
/// </summary>
public record ControlPropertyGroup(
    string Prefix,
    string Name,
    PropertyUsage Usage = PropertyUsage.Attribute,
    PropertyValue Value = PropertyValue.Any,
    string? TypeName = null)
{
    /// <summary>Whether it can be written as an attribute at all.</summary>
    public bool IsAttribute => Usage is PropertyUsage.Attribute or PropertyUsage.Both;
}
