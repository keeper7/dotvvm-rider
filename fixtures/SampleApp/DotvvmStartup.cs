using DotVVM.Framework.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SampleApp
{
    /// <summary>
    /// Registration of prefixes and controls. This is exactly where the language server's probe
    /// process reads them from; prefixes are not declared in a .dothtml file.
    /// </summary>
    public class DotvvmStartup : IDotvvmStartup, IDotvvmServiceConfigurator
    {
        public void Configure(DotvvmConfiguration config, string applicationPath)
        {
            config.RouteTable.Add("Sample", "", "Views/Sample.dothtml");

            config.Markup.AddMarkupControl("cc", "MyControl", "Controls/MyControl.dotcontrol");
        }

        public void ConfigureServices(IDotvvmServiceCollection options)
        {
        }
    }
}
