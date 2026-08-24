using DotVVM.Framework.Compilation;
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

            // A route with a parameter, so that a RouteLink can carry the Param- family
            config.RouteTable.Add("Detail", "detail/{Id}", "Views/Sample.dothtml");

            config.Markup.AddMarkupControl("cc", "MyControl", "Controls/MyControl.dotcontrol");
            config.Markup.AddMarkupControl("cc", "Address", "Controls/Address.dotcontrol");

            // A namespace every view has without importing it. This is how a real project puts
            // its resource classes in scope, and the reason completion has to read the
            // configuration and not only a file's own @import - Address.dotcontrol declares
            // none and still writes `{resource: Labels.Street}`.
            config.Markup.ImportedNamespaces.Add(new NamespaceImport("SampleApp.Resources"));
        }

        public void ConfigureServices(IDotvvmServiceCollection options)
        {
        }
    }
}
