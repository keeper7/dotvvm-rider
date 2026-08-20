using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using DotVVM.Framework.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DotVVM.LanguageServer.Probe;

/// <summary>
/// Načte sestavenou assembly DotVVM projektu, získá z ní konfiguraci
/// a vypíše registrace kontrolek jako JSON. Replikuje postup oficiálního
/// DotVVM.Compiler, protože ConfigurationInitializer je internal.
///
/// Běží jako samostatný proces: pád uživatelského DotvvmStartup ani zamčený
/// soubor assembly tak neovlivní jazykový server.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("použití: probe <cesta-k-assembly> <adresář-projektu>");
            return 2;
        }

        var assemblyPath = args[0];
        var projectDir = args[1];

        try
        {
            var assembly = LoadWithDependencies(assemblyPath);
            var config = BuildConfiguration(assembly, projectDir);
            Console.Out.Write(JsonSerializer.Serialize(Extract(config)));
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"probe selhal: {ex.GetType().Name}: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Načte assembly i s jejími závislostmi. Tím se použije verze DotVVM,
    /// kterou má cílový projekt — nikoli ta, proti které byl probe sestaven.
    /// </summary>
    private static Assembly LoadWithDependencies(string assemblyPath)
    {
        var resolver = new AssemblyDependencyResolver(assemblyPath);

        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            var path = resolver.ResolveAssemblyToPath(name);
            return path is null ? null : context.LoadFromAssemblyPath(path);
        };

        return AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(assemblyPath));
    }

    /// <summary>
    /// Vrátí typy, které šlo načíst. Velká aplikace obvykle obsahuje typy odkazující
    /// na assembly, které tu nejsou k dispozici; GetTypes() by kvůli jedinému takovému
    /// typu shodil celý rozbor, ačkoli DotvvmStartup načíst jde.
    /// </summary>
    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!;
        }
    }

    private static DotvvmConfiguration BuildConfiguration(Assembly assembly, string projectDir)
    {
        // Uživatelské DotvvmStartup si běžně tahá IConfiguration z DI. CreateDefault()
        // ji neregistruje, takže by rozbor skončil na "No service for type IConfiguration".
        // Prázdná konfigurace stačí — z konfigurace se čtou hodnoty, ne registrace kontrolek.
        var config = DotvvmConfiguration.CreateDefault(services =>
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build()));
        config.ApplicationPhysicalPath = projectDir;

        var startupType = GetLoadableTypes(assembly).FirstOrDefault(t =>
            typeof(IDotvvmStartup).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false });

        if (startupType is null)
        {
            throw new InvalidOperationException(
                $"v assembly '{assembly.GetName().Name}' není implementace IDotvvmStartup");
        }

        var startup = (IDotvvmStartup)Activator.CreateInstance(startupType)!;
        startup.Configure(config, projectDir);
        return config;
    }

    private static ProbeResult Extract(DotvvmConfiguration config)
    {
        var registrations = config.Markup.Controls.Select(c => new ProbeRegistration(
            TagPrefix: c.TagPrefix ?? "",
            Namespace: c.Namespace,
            Assembly: c.Assembly,
            TagName: c.TagName,
            Src: c.Src)).ToList();

        return new ProbeResult(registrations);
    }
}

public record ProbeRegistration(
    string TagPrefix, string? Namespace, string? Assembly, string? TagName, string? Src);

public record ProbeResult(List<ProbeRegistration> Registrations);
