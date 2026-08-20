using DotVVM.Framework.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SampleApp
{
    /// <summary>
    /// Registrace prefixů a kontrolek. Právě odsud si je bere probe proces
    /// jazykového serveru — v souboru .dothtml se prefixy nedeklarují.
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
