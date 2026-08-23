using DotVVM.Framework.Binding;
using DotVVM.Framework.Controls;

namespace SampleApp.Controls;

/// <summary>
/// The code-behind of Address.dotcontrol, named by its @baseType directive. A markup control
/// declares no properties of its own, so this class is the only route to them.
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
}
