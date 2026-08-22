using DotVVM.Framework.Binding;
using DotVVM.Framework.Controls;

namespace SampleApp.Controls
{
    /// <summary>
    /// The code-behind class of the cc:MyControl markup control. The .dotcontrol file names it
    /// with @baseType, and it is the only place the control's properties exist - which is what
    /// makes this fixture worth having: without it nothing proves the resolution works.
    /// </summary>
    public class MyControl : DotvvmMarkupControl
    {
        public string? Caption
        {
            get => (string?)GetValue(CaptionProperty);
            set => SetValue(CaptionProperty, value);
        }

        public static readonly DotvvmProperty CaptionProperty
            = DotvvmProperty.Register<string?, MyControl>(c => c.Caption);
    }
}
