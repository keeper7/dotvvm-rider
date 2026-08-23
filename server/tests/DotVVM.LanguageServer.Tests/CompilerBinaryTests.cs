using DotVVM.LanguageServer.Compilation;
using Xunit;

namespace DotVVM.LanguageServer.Tests;

public class CompilerBinaryTests
{
    private static Func<string, bool> Present(params string[] frameworks) =>
        path => frameworks.Any(tfm => path.Contains(Path.Combine("compiler", tfm)));

    [Fact]
    public void PicksTheVariantMatchingTheTargetFramework()
    {
        var path = CompilerBinary.Resolve("net8.0", Present("net8.0", "net9.0"));

        Assert.Contains(Path.Combine("compiler", "net8.0"), path);
    }

    /// <summary>
    /// A net8 runtime cannot load a net9 assembly, so the variant has to be at least as new as
    /// the project - the same rule the probe follows.
    /// </summary>
    [Fact]
    public void NeverPicksAVariantOlderThanTheProject()
    {
        var path = CompilerBinary.Resolve("net9.0", Present("net8.0", "net9.0"));

        Assert.Contains(Path.Combine("compiler", "net9.0"), path);
    }

    [Fact]
    public void FallsBackToTheNewestWhenTheFrameworkIsUnknown()
    {
        var path = CompilerBinary.Resolve(null, Present("net8.0", "net9.0"));

        Assert.Contains(Path.Combine("compiler", "net9.0"), path);
    }

    [Fact]
    public void SaysNothingWhenNoVariantIsPresent()
    {
        Assert.Null(CompilerBinary.Resolve("net8.0", _ => false));
    }

    /// <summary>
    /// The deps and runtimeconfig of the target application are the whole trick: DotVVM's
    /// CompiledAssemblyCache reads DependencyContext.Default, which comes from the entry
    /// assembly's deps.json, and with our own the project's assembly is missing from it.
    /// </summary>
    [Fact]
    public void RunsWithTheApplicationsOwnDepsAndRuntimeConfig()
    {
        var arguments = CompilerBinary.BuildArguments(
            "/plugin/compiler/net8.0/DotVVM.LanguageServer.Compiler.dll",
            "/app/bin/Debug/net8.0/App.dll",
            "/app");

        Assert.Equal("exec", arguments[0]);
        Assert.Contains("--depsfile", arguments);
        Assert.Contains(Path.Combine("/app/bin/Debug/net8.0", "App.deps.json"), arguments);
        Assert.Contains("--runtimeconfig", arguments);
        Assert.Contains(Path.Combine("/app/bin/Debug/net8.0", "App.runtimeconfig.json"), arguments);
        Assert.Equal("/app", arguments[^1]);
    }

    [Fact]
    public void RefusesAnApplicationWithoutThoseFiles()
    {
        Assert.False(CompilerBinary.CanRunAgainst("/app/bin/App.dll", _ => false));
        Assert.True(CompilerBinary.CanRunAgainst("/app/bin/App.dll", _ => true));
    }
}

public class LiveValidationSwitchTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("on")]
    [InlineData("1")]
    public void StaysOnByDefault(string? value) =>
        Assert.True(LiveValidation.IsEnabled(value));

    [Theory]
    [InlineData("off")]
    [InlineData("OFF")]
    [InlineData("false")]
    [InlineData("0")]
    public void GoesOffWhenAsked(string value) =>
        Assert.False(LiveValidation.IsEnabled(value));
}
