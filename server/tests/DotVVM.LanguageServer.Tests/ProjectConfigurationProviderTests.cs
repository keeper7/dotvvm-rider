using DotVVM.LanguageServer.Configuration;
using DotVVM.LanguageServer.Model;
using Xunit;

namespace DotVVM.LanguageServer.Tests;

public class ProjectConfigurationProviderTests
{
    private sealed class FakeSource : IConfigurationSource
    {
        private readonly ControlRegistry? _registry;
        public FakeSource(string name, ControlRegistry? registry, bool knowsProjectPrefixes = false)
        { Name = name; _registry = registry; KnowsProjectPrefixes = knowsProjectPrefixes; }
        public string Name { get; }
        public bool KnowsProjectPrefixes { get; }
        public Task<ControlRegistry?> LoadAsync(string dir, CancellationToken ct) =>
            Task.FromResult(_registry);
    }

    private static ControlRegistry RegistryWithPrefix(string prefix) => new(
        new[] { new ControlRegistration(prefix, $"Ns.{prefix}", "Asm", null, null) },
        new[] { new ControlInfo($"Ns.{prefix}.Thing", null, null, new[] { "P" }) });

    [Fact]
    public async Task UsesHighestAvailableTier()
    {
        var provider = new ProjectConfigurationProvider(new IConfigurationSource[]
        {
            new FakeSource("built-in", RegistryWithPrefix("dot")),
            new FakeSource("config", RegistryWithPrefix("cc")),
            new FakeSource("assembly", RegistryWithPrefix("full")),
        });

        var result = await provider.GetAsync("/x", default);
        Assert.Equal("assembly", result.SourceName);
    }

    [Fact]
    public async Task FallsBackWhenHigherTierUnavailable()
    {
        var provider = new ProjectConfigurationProvider(new IConfigurationSource[]
        {
            new FakeSource("built-in", RegistryWithPrefix("dot")),
            new FakeSource("config", RegistryWithPrefix("cc")),
            new FakeSource("assembly", null),
        });

        var result = await provider.GetAsync("/x", default);
        Assert.Equal("config", result.SourceName);
    }

    [Fact]
    public async Task MergesLowerTiersIntoResult()
    {
        var provider = new ProjectConfigurationProvider(new IConfigurationSource[]
        {
            new FakeSource("built-in", RegistryWithPrefix("dot")),
            new FakeSource("config", RegistryWithPrefix("cc")),
        });

        var result = await provider.GetAsync("/x", default);
        // vyšší stupeň nesmí zahodit znalosti nižšího
        Assert.True(result.Registry.IsKnownPrefix("dot"));
        Assert.True(result.Registry.IsKnownPrefix("cc"));
    }

    [Fact]
    public async Task ReturnsEmptyRegistryWhenNoSourceAvailable()
    {
        var provider = new ProjectConfigurationProvider(new IConfigurationSource[]
        {
            new FakeSource("built-in", null),
        });

        var result = await provider.GetAsync("/x", default);
        Assert.False(result.Registry.IsKnownPrefix("dot"));
        Assert.Equal("none", result.SourceName);
    }

    [Fact]
    public async Task FailingSourceDoesNotBreakProvider()
    {
        var provider = new ProjectConfigurationProvider(new IConfigurationSource[]
        {
            new FakeSource("built-in", RegistryWithPrefix("dot")),
            new ThrowingSource(),
        });

        var result = await provider.GetAsync("/x", default);
        Assert.Equal("built-in", result.SourceName);
    }

    [Fact]
    public async Task ProjectPrefixesAreUnknownWhenOnlyBuiltInsLoad()
    {
        var provider = new ProjectConfigurationProvider(new IConfigurationSource[]
        {
            new FakeSource("built-in", RegistryWithPrefix("dot")),
            new FakeSource("config", null, knowsProjectPrefixes: true),
        });

        var result = await provider.GetAsync("/x", default);
        Assert.False(result.KnowsProjectPrefixes);
    }

    [Fact]
    public async Task ProjectPrefixesAreKnownOnceAHigherTierLoads()
    {
        var provider = new ProjectConfigurationProvider(new IConfigurationSource[]
        {
            new FakeSource("built-in", RegistryWithPrefix("dot")),
            new FakeSource("config", RegistryWithPrefix("cc"), knowsProjectPrefixes: true),
        });

        var result = await provider.GetAsync("/x", default);
        Assert.True(result.KnowsProjectPrefixes);
    }

    private sealed class ThrowingSource : IConfigurationSource
    {
        public string Name => "broken";
        public bool KnowsProjectPrefixes => true;
        public Task<ControlRegistry?> LoadAsync(string dir, CancellationToken ct) =>
            throw new InvalidOperationException("simulated failure");
    }
}
