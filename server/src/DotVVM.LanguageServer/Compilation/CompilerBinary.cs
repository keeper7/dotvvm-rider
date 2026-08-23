namespace DotVVM.LanguageServer.Compilation;

/// <summary>
/// Where the compiler process lives and how it has to be started. Free of any process handling
/// so the decisions can be tested without launching anything.
/// </summary>
public static class CompilerBinary
{
    /// <summary>Variants, oldest first; a newer runtime also loads an older assembly.</summary>
    public static readonly string[] Frameworks = { "net8.0", "net9.0" };

    public const string FileName = "DotVVM.LanguageServer.Compiler.dll";

    public static string Root => Path.Combine(AppContext.BaseDirectory, "compiler");

    /// <summary>
    /// Picks the variant by the target assembly's own framework, the same rule the probe follows:
    /// a net8.0 host cannot load a net9.0 assembly, while the other direction is fine.
    /// </summary>
    public static string? Resolve(string? targetFramework, Func<string, bool> exists)
    {
        var candidates = Frameworks
            .Select(tfm => (Tfm: tfm, Path: Path.Combine(Root, tfm, FileName)))
            .Where(c => exists(c.Path))
            .ToList();

        if (candidates.Count == 0) return null;

        var index = Array.IndexOf(Frameworks, targetFramework);
        if (index >= 0)
        {
            var match = candidates.FirstOrDefault(
                c => Array.IndexOf(Frameworks, c.Tfm) >= index);
            if (match.Path is not null) return match.Path;
        }

        return candidates[^1].Path;
    }

    /// <summary>
    /// The arguments for `dotnet`. The deps and runtimeconfig of the **target application** are
    /// what make this work at all: DotVVM's CompiledAssemblyCache reads DependencyContext.Default,
    /// which comes from the entry assembly's deps.json, and with our own the project's assembly
    /// is missing from it - DefaultControlResolver then throws in its constructor before a single
    /// view is compiled. Measured: 329 assemblies loaded in the process, 317 in that list, the
    /// project's not among them.
    /// </summary>
    public static IReadOnlyList<string> BuildArguments(
        string compilerPath, string assemblyPath, string projectDir)
    {
        var withoutExtension = Path.Combine(
            Path.GetDirectoryName(assemblyPath) ?? ".",
            Path.GetFileNameWithoutExtension(assemblyPath));

        return new[]
        {
            "exec",
            "--depsfile", withoutExtension + ".deps.json",
            "--runtimeconfig", withoutExtension + ".runtimeconfig.json",
            compilerPath,
            assemblyPath,
            projectDir,
        };
    }

    /// <summary>Whether the application has the files the arguments above name.</summary>
    public static bool CanRunAgainst(string assemblyPath, Func<string, bool> exists)
    {
        var withoutExtension = Path.Combine(
            Path.GetDirectoryName(assemblyPath) ?? ".",
            Path.GetFileNameWithoutExtension(assemblyPath));

        return exists(withoutExtension + ".deps.json") &&
               exists(withoutExtension + ".runtimeconfig.json");
    }
}
