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

        var assemblies = AssembliesToScan(config, assembly).ToList();

        return new ProbeResult(
            registrations, ExtractControls(assemblies), ExtractAttachedProperties(assemblies));
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
                .Where(IsWritableInMarkup)
                .Select(Describe)
                .OrderBy(p => p.Name, StringComparer.Ordinal)
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
    /// Properties written as Owner.Name on any element. DotVVM marks them with
    /// AttachedPropertyAttribute on the static field, and that marker is the only reliable sign:
    /// measured on DotVVM 4.3.17 it yields exactly the 26 the framework itself serializes as
    /// attached, while "no backing PropertyInfo" yields 54 (dragging in the Internal.* plumbing)
    /// and "declared outside a control" yields 38 while losing Validator.*, used 503 times in a
    /// real project.
    /// </summary>
    private static List<ProbeProperty> ExtractAttachedProperties(IEnumerable<Assembly> assemblies)
    {
        var result = new List<ProbeProperty>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var type in assemblies.SelectMany(GetLoadableTypes))
        {
            if (type.IsGenericTypeDefinition) continue;

            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => typeof(DotvvmProperty).IsAssignableFrom(f.FieldType) &&
                            f.GetCustomAttribute<AttachedPropertyAttribute>() is not null)
                .ToList();

            if (fields.Count == 0) continue;
            if (!RunConstructorChain(type)) continue;

            foreach (var field in fields)
            {
                if (field.GetValue(null) is not DotvvmProperty property) continue;
                if (!IsWritableInMarkup(property)) continue;

                var described = Describe(property) with
                {
                    Name = $"{property.DeclaringType.Name}.{property.Name}"
                };
                if (seen.Add(described.Name)) result.Add(described);
            }
        }

        return result.OrderBy(p => p.Name, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Whether the property can appear in markup at all. Measured over the framework's controls:
    /// of 614 properties, 50 are MappingMode.Exclude (ClientID and friends) and 45 are capability
    /// containers (HtmlCapability) that hold other properties rather than taking a value.
    /// Offering either would be an error, and hover listed them until now.
    /// </summary>
    private static bool IsWritableInMarkup(DotvvmProperty property) =>
        property is not DotvvmCapabilityProperty &&
        (property.MarkupOptions?.MappingMode ?? MappingMode.Attribute) != MappingMode.Exclude;

    private static ProbeProperty Describe(DotvvmProperty property)
    {
        var options = property.MarkupOptions;
        var mode = options?.MappingMode ?? MappingMode.Attribute;
        var allowBinding = options?.AllowBinding ?? true;
        var allowHardCoded = options?.AllowHardCodedValue ?? true;

        return new ProbeProperty(
            Name: property.Name,
            Usage: mode switch
            {
                MappingMode.InnerElement => "InnerElement",
                MappingMode.Both => "Both",
                _ => "Attribute",
            },
            Value: allowBinding && !allowHardCoded ? "BindingOnly"
                 : !allowBinding && allowHardCoded ? "HardCodedOnly"
                 : "Any",
            Required: options?.Required ?? false,
            TypeName: property.PropertyType.FullName);
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

public record ProbeProperty(
    string Name, string Usage, string Value, bool Required, string? TypeName);

public record ProbeControl(
    string FullTypeName, string? BaseType, string? DefaultContentProperty,
    List<ProbeProperty> Properties);

public record ProbeResult(
    List<ProbeRegistration> Registrations,
    List<ProbeControl> Controls,
    List<ProbeProperty> AttachedProperties);
