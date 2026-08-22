namespace DotVVM.LanguageServer.Model;

/// <summary>Where the property may be written in markup.</summary>
public enum PropertyUsage { Attribute, InnerElement, Both }

/// <summary>What its value may be.</summary>
public enum PropertyValue { Any, BindingOnly, HardCodedOnly }

/// <summary>
/// A property of a control. The name alone is not enough to offer it: 44 of the framework's 614
/// properties are written as a child element rather than an attribute, and some accept only a
/// binding. The defaults describe the common case, so a source that knows no more than the name
/// stays terse.
/// </summary>
public record ControlProperty(
    string Name,
    PropertyUsage Usage = PropertyUsage.Attribute,
    PropertyValue Value = PropertyValue.Any,
    bool Required = false,
    string? TypeName = null)
{
    /// <summary>Whether it can be written as an attribute at all.</summary>
    public bool IsAttribute => Usage is PropertyUsage.Attribute or PropertyUsage.Both;
}
