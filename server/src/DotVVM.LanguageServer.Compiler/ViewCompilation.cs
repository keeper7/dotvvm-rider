using DotVVM.Framework.Compilation;
using DotVVM.Framework.Compilation.ViewCompiler;
using DotVVM.Framework.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DotVVM.LanguageServer.Compiler;

/// <summary>
/// Runs DotVVM's own view compiler over a file and turns what it says into diagnostics. Knows
/// nothing about the protocol, so it can be reasoned about on its own.
/// </summary>
public sealed class ViewCompilation
{
    private readonly IViewCompiler _compiler;
    private readonly DefaultControlBuilderFactory? _builders;

    public ViewCompilation(DotvvmConfiguration configuration)
    {
        _compiler = configuration.ServiceProvider.GetRequiredService<IViewCompiler>();
        // InvalidateCache sits on the implementation, not on IControlBuilderFactory. A different
        // factory would leave us without invalidation rather than without a compiler.
        _builders = configuration.ServiceProvider.GetRequiredService<IControlBuilderFactory>()
                    as DefaultControlBuilderFactory;
    }

    public IReadOnlyList<CompileDiagnostic> Compile(string path, string text)
    {
        // The compiler caches by file name, so without this the second run of a file being
        // edited would report the errors of the version before it.
        try { _builders?.InvalidateCache(path); }
        catch (Exception) { /* nothing cached under that name yet */ }

        try
        {
            var (_, builder) = _compiler.CompileView(text, path);

            // CompileView is lazy: it hands back a Func and compiles nothing until it is called,
            // so without this even a plainly broken file comes back clean.
            builder();
            return Array.Empty<CompileDiagnostic>();
        }
        catch (DotvvmCompilationException ex)
        {
            return ex.AllDiagnostics.Select(Describe).ToList();
        }
    }

    private static CompileDiagnostic Describe(DotvvmCompilationDiagnostic diagnostic) =>
        new(Severity: diagnostic.Severity.ToString(),
            Message: diagnostic.Message,
            StartLine: diagnostic.Location?.LineNumber,
            StartColumn: diagnostic.Location?.ColumnNumber,
            EndLine: diagnostic.Location?.EndLineNumber,
            EndColumn: diagnostic.Location?.EndColumnNumber);
}
