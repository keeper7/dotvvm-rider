using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text.Json;
using DotVVM.Framework.Configuration;
using DotVVM.Framework.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DotVVM.LanguageServer.Compiler;

/// <summary>
/// Everything that touches DotVVM. Kept apart from <see cref="Program"/> on purpose: the JIT
/// compiles a method the first time it is called, which here is after the assembly resolver is
/// registered - see the note in Program.Main.
/// </summary>
internal static class Session
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Run(string assemblyPath, string projectDir)
    {
        ViewCompilation compilation;
        MemberCompletion completion;
        try
        {
            var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(
                Path.GetFullPath(assemblyPath));
            var configuration = BuildConfiguration(assembly, projectDir);
            compilation = new ViewCompilation(configuration);
            completion = new MemberCompletion(configuration);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"compiler failed to start: {ex.GetType().Name}: {ex.Message}");
            return 1;
        }

        WarmUp(compilation);
        Serve(compilation, completion);
        return 0;
    }

    /// <summary>
    /// Compiles a trivial view so the seconds Roslyn takes to wake up are spent before the first
    /// real request rather than during it. The line printed afterwards is what the server waits
    /// for before sending anything.
    /// </summary>
    private static void WarmUp(ViewCompilation compilation)
    {
        try
        {
            compilation.Compile("__warmup.dothtml",
                                "@viewModel System.Object\n<html><body></body></html>");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"compiler warm-up failed: {ex.GetType().Name}: {ex.Message}");
        }

        Console.Out.WriteLine("{\"ready\":true}");
        Console.Out.Flush();
    }

    /// <summary>One request per line in, one response per line out; the text is JSON-escaped.</summary>
    private static void Serve(ViewCompilation compilation, MemberCompletion completion)
    {
        while (Console.In.ReadLine() is { } line)
        {
            if (line.Length == 0) continue;

            CompileRequest? request = null;
            CompileResponse response;
            try
            {
                request = JsonSerializer.Deserialize(line, ProtocolContext.Default.CompileRequest);
                if (request is null) continue;

                response = request.Kind == Kinds.Complete
                    ? new CompileResponse(
                        request.Id,
                        new List<CompileDiagnostic>(),
                        completion.Complete(request.Path, request.Text, request.Offset,
                                            request.Expression, request.Binding).ToList())
                    : new CompileResponse(
                        request.Id, compilation.Compile(request.Path, request.Text).ToList());
            }
            catch (Exception ex)
            {
                // A view can fail in ways the compiler does not turn into diagnostics - the
                // user's own converters and validators run here. One bad file must not end the
                // process, or every later request would go unanswered.
                response = new CompileResponse(
                    request?.Id ?? 0, new List<CompileDiagnostic>(), null,
                    $"{ex.GetType().Name}: {ex.Message}");
            }

            Console.Out.WriteLine(
                JsonSerializer.Serialize(response, ProtocolContext.Default.CompileResponse));
            Console.Out.Flush();
        }
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t is not null)!; }
    }

    private static DotvvmConfiguration BuildConfiguration(Assembly assembly, string projectDir)
    {
        var config = DotvvmConfiguration.CreateDefault(services =>
        {
            // A user's DotvvmStartup routinely resolves IConfiguration from DI
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
            // Every staticCommand needs this one, and CreateDefault does not provide it
            services.AddSingleton<IViewModelProtector, ViewModelProtectorStub>();
        });
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
}
