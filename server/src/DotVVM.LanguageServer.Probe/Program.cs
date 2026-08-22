using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using DotVVM.Framework.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DotVVM.LanguageServer.Probe;

/// <summary>
/// Loads the compiled assembly of a DotVVM project, obtains its configuration and prints the
/// control registrations as JSON. It mirrors what the official DotVVM.Compiler does, because
/// ConfigurationInitializer is internal.
///
/// It runs as a separate process, so neither a crash in the user's DotvvmStartup nor a locked
/// assembly file can affect the language server.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("usage: probe <assembly-path> <project-directory>");
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
            Console.Error.WriteLine($"probe failed: {ex.GetType().Name}: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Loads the assembly together with its dependencies, so the DotVVM version used is the one
    /// the target project has, not the one the probe was built against.
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
    /// Returns the types that could be loaded. A large application usually contains types
    /// referencing assemblies that are not available here; GetTypes() would fail the whole scan
    /// over a single such type, even though DotvvmStartup itself loads fine.
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
        // A user's DotvvmStartup routinely resolves IConfiguration from DI. CreateDefault()
        // does not register it, so the scan would fail with "No service for type IConfiguration".
        // An empty configuration is enough: it supplies values, not control registrations.
        var config = DotvvmConfiguration.CreateDefault(services =>
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build()));
        config.ApplicationPhysicalPath = projectDir;

        var startupType = GetLoadableTypes(assembly).FirstOrDefault(t =>
            typeof(IDotvvmStartup).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false });

        if (startupType is null)
        {
            throw new InvalidOperationException(
                $"assembly '{assembly.GetName().Name}' contains no IDotvvmStartup implementation");
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
