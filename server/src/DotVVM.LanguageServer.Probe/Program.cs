using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text.Json;
using DotVVM.Framework.Binding;
using DotVVM.Framework.Configuration;
using DotVVM.Framework.Controls;
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
            Console.Out.Write(JsonSerializer.Serialize(Extract(config, assembly)));
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

    private static ProbeResult Extract(DotvvmConfiguration config, Assembly assembly)
    {
        var registrations = config.Markup.Controls.Select(c => new ProbeRegistration(
            TagPrefix: c.TagPrefix ?? "",
            Namespace: c.Namespace,
            Assembly: c.Assembly,
            TagName: c.TagName,
            Src: c.Src)).ToList();

        return new ProbeResult(registrations, ExtractControls(AssembliesToScan(config, assembly)));
    }

    /// <summary>
    /// The project's own assembly plus every assembly a registration names. Without the latter
    /// the standard controls would have no properties at this tier: dot:Repeater lives in
    /// DotVVM.Framework, not in the project. An assembly that fails to load is skipped - the
    /// registration stays valid, only its properties stay unknown.
    /// </summary>
    private static IEnumerable<Assembly> AssembliesToScan(
        DotvvmConfiguration config, Assembly project)
    {
        var result = new List<Assembly> { project };

        var names = config.Markup.Controls
            .Select(c => c.Assembly)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var name in names)
        {
            try
            {
                var loaded = Assembly.Load(new AssemblyName(name!));
                if (!result.Contains(loaded)) result.Add(loaded);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"probe: cannot load '{name}': {ex.GetType().Name}");
            }
        }

        return result;
    }

    /// <summary>
    /// Reads the control types together with the properties they declare.
    ///
    /// DotvvmProperty instances are registered from static fields, so a type whose class
    /// constructor has not run reports no properties at all - and running it for the type alone
    /// is not enough either: the inherited ones (Visible, DataContext, ID, IncludeInPage) come
    /// from the base classes. Measured on dot:Repeater: 0 properties without this, 6 with the
    /// type alone, 15 with the whole chain.
    /// </summary>
    private static List<ProbeControl> ExtractControls(IEnumerable<Assembly> assemblies)
    {
        var result = new List<ProbeControl>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var type in assemblies.SelectMany(GetLoadableTypes))
        {
            if (type.IsAbstract || type.IsInterface) continue;
            // Neither can ever appear as a tag, and their names would only pollute completion:
            // a nested type carries the '+' separator, a generic definition the arity backtick.
            if (type.IsNested || type.IsGenericTypeDefinition) continue;
            if (!typeof(DotvvmBindableObject).IsAssignableFrom(type)) continue;

            if (!seen.Add(type.FullName ?? type.Name)) continue;
            if (!RunConstructorChain(type)) continue;

            var properties = DotvvmProperty.ResolveProperties(type)
                .Select(p => p.Name)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();

            result.Add(new ProbeControl(
                FullTypeName: type.FullName ?? type.Name,
                BaseType: type.BaseType?.FullName,
                DefaultContentProperty: type
                    .GetCustomAttribute<ControlMarkupOptionsAttribute>()?.DefaultContentProperty,
                Properties: properties));
        }

        return result;
    }

    /// <summary>
    /// Runs the class constructors up the whole chain. This executes the user's own code, so a
    /// control that fails to initialise is skipped rather than allowed to end the scan.
    /// </summary>
    private static bool RunConstructorChain(Type type)
    {
        for (var t = type; t is not null && t != typeof(object); t = t.BaseType)
        {
            try
            {
                RuntimeHelpers.RunClassConstructor(t.TypeHandle);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"probe: skipping '{type.FullName}': {ex.GetType().Name}");
                return false;
            }
        }
        return true;
    }
}

public record ProbeRegistration(
    string TagPrefix, string? Namespace, string? Assembly, string? TagName, string? Src);

public record ProbeControl(
    string FullTypeName, string? BaseType, string? DefaultContentProperty, List<string> Properties);

public record ProbeResult(List<ProbeRegistration> Registrations, List<ProbeControl> Controls);
