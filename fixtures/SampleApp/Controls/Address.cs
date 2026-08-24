using DotVVM.Framework.Binding;
using DotVVM.Framework.Controls;

namespace SampleApp.Controls;

/// <summary>
/// The code-behind of Address.dotcontrol, named by its @baseType directive. A markup control
/// declares no properties of its own, so this class is the only route to them - which is what
/// makes the pair worth having: the resolution cannot be proved without both halves.
/// </summary>
public class Address : DotvvmMarkupControl
{
    public static readonly DotvvmProperty StreetProperty =
        DotvvmProperty.Register<string, Address>(c => c.Street, string.Empty);

    public string Street
    {
        get => (string)GetValue(StreetProperty)!;
        set => SetValue(StreetProperty, value);
    }

    public static readonly DotvvmProperty CityProperty =
        DotvvmProperty.Register<string, Address>(c => c.City, string.Empty);

    public string City
    {
        get => (string)GetValue(CityProperty)!;
        set => SetValue(CityProperty, value);
    }

    public static readonly DotvvmProperty PostalCodeProperty =
        DotvvmProperty.Register<string, Address>(c => c.PostalCode, string.Empty);

    public string PostalCode
    {
        get => (string)GetValue(PostalCodeProperty)!;
        set => SetValue(PostalCodeProperty, value);
    }

    /// <summary>Bound by a property family on a plain label - `Class-required`.</summary>
    public static readonly DotvvmProperty RequiredProperty =
        DotvvmProperty.Register<bool, Address>(c => c.Required, false);

    public bool Required
    {
        get => (bool)GetValue(RequiredProperty)!;
        set => SetValue(RequiredProperty, value);
    }

    public static readonly DotvvmProperty EnabledProperty =
        DotvvmProperty.Register<bool, Address>(c => c.Enabled, true);

    public bool Enabled
    {
        get => (bool)GetValue(EnabledProperty)!;
        set => SetValue(EnabledProperty, value);
    }
}
