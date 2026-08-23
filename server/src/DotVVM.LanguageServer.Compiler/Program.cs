using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace DotVVM.LanguageServer.Compiler;

/// <summary>
/// Compiles views with DotVVM's own compiler and reports what it says, so the editor can show
/// the errors a build would - a mistyped property in a binding, a wrong data context, a value
/// of the wrong type.
///
/// Unlike the probe, which runs once and exits, this process **stays alive**: the first
/// compilation costs seconds of Roslyn warm-up and every one after it a few milliseconds, so a
/// process per request would be unusable.
///
/// It has to be started through `dotnet exec --depsfile &lt;app&gt;.deps.json --runtimeconfig
/// &lt;app&gt;.runtimeconfig.json`. DotVVM's CompiledAssemblyCache reads DependencyContext.Default,
/// which comes from the entry assembly's deps.json; with our own, the project's assembly is
/// missing from it and DefaultControlResolver throws in its constructor before anything is
/// compiled at all.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("usage: compiler <assembly-path> <project-directory>");
            return 2;
        }

        // **Nothing in this method may touch a DotVVM type.** The reference is built against the
        // newest DotVVM while the project may be on an older one, and the JIT resolves the types
        // a method mentions when it compiles that method - so a mention here would demand
        // exactly this version before the resolver below exists, and the process would die with
        // "Could not load DotVVM.Framework, Version=4.3.17.0" before its first line ran.
        // Measured: without the split, a build against 4.3.17 does not run against a project on
        // 4.3.6 at all, while with it the same build serves both.
        try
        {
            RegisterAssemblyResolver(args[0]);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"compiler failed to start: {ex.GetType().Name}: {ex.Message}");
            return 1;
        }

        return Session.Run(args[0], args[1]);
    }

    /// <summary>
    /// Points the runtime at the target application's own assemblies, the DotVVM it was built
    /// against included. Resolution goes by name, so the version compiled against need not match.
    /// </summary>
    private static void RegisterAssemblyResolver(string assemblyPath)
    {
        var resolver = new AssemblyDependencyResolver(assemblyPath);

        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            var path = resolver.ResolveAssemblyToPath(name);
            return path is null ? null : context.LoadFromAssemblyPath(path);
        };
    }
}
