using DotVVM.LanguageServer.Configuration;
using Xunit;

namespace DotVVM.LanguageServer.Tests;

public class AssemblyProbeSourceTests
{
    [Fact]
    public async Task ReturnsNullWhenNoAssemblyFound()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dotvvm-probe-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            Assert.Null(await new AssemblyProbeSource().LoadAsync(dir, default));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task ReturnsNullWhenProbeExecutableMissing()
    {
        // The source must not throw when the probe is unavailable
        var source = new AssemblyProbeSource(probePath: "/nonexistent/probe");
        Assert.Null(await source.LoadAsync(Path.GetTempPath(), default));
    }

    [Fact]
    public void ParsesProbeOutput()
    {
        const string json = """
            {"Registrations":[
              {"TagPrefix":"dot","Namespace":"DotVVM.Framework.Controls","Assembly":"DotVVM.Framework","TagName":null,"Src":null},
              {"TagPrefix":"cc","Namespace":null,"Assembly":null,"TagName":"Address","Src":"Controls/Address.dotcontrol"}
            ]}
            """;

        var registry = AssemblyProbeSource.ParseProbeOutput(json);
        Assert.NotNull(registry);
        Assert.True(registry!.IsKnownPrefix("dot"));
        Assert.True(registry.IsKnownTag("cc", "Address"));
    }

    [Fact]
    public void ParsesControlsWithTheirProperties()
    {
        const string json = """
            {
              "Registrations": [
                {"TagPrefix":"dot","Namespace":"DotVVM.Framework.Controls","Assembly":"DotVVM.Framework","TagName":null,"Src":null}
              ],
              "Controls": [
                {"FullTypeName":"DotVVM.Framework.Controls.Repeater",
                 "BaseType":"DotVVM.Framework.Controls.ItemsControl",
                 "DefaultContentProperty":null,
                 "Properties":["DataContext","DataSource","ItemTemplate","Visible"]}
              ]
            }
            """;

        var registry = AssemblyProbeSource.ParseProbeOutput(json);

        var control = registry!.GetControl("dot", "Repeater");
        Assert.NotNull(control);
        Assert.Contains("Visible", control!.Properties);
        Assert.Contains("ItemTemplate", control.Properties);
    }

    /// <summary>
    /// Output from an older probe carries no Controls key at all. The registrations must still
    /// load: a stale bundled probe would otherwise take tier 3 down entirely.
    /// </summary>
    [Fact]
    public void ParsesOutputWithoutTheControlsKey()
    {
        const string json = """
            {"Registrations":[
              {"TagPrefix":"dot","Namespace":"DotVVM.Framework.Controls","Assembly":"DotVVM.Framework","TagName":null,"Src":null}
            ]}
            """;

        var registry = AssemblyProbeSource.ParseProbeOutput(json);

        Assert.NotNull(registry);
        Assert.True(registry!.IsKnownPrefix("dot"));
    }

    [Fact]
    public void ReturnsNullForMalformedProbeOutput()
    {
        Assert.Null(AssemblyProbeSource.ParseProbeOutput("{ broken"));
    }

    /// <summary>
    /// The probe must build into the probe/ subdirectory of the server output, which is where
    /// DefaultProbePath() looks for it at run time via AppContext.BaseDirectory. If it stopped
    /// being copied, LoadAsync would silently return null and tier 3 would never start. The
    /// server output is checked, not the test one: only the server dll is copied into the test
    /// output directory, not the probe/ subdirectory.
    /// </summary>
    [Fact]
    public void ProbeIsDeployedIntoServerOutput()
    {
        Assert.NotEmpty(FindDeployedProbes());
    }

    /// <summary>
    /// A live check of the whole of tier 3: runs the probe against the built fixture
    /// application. Skipped until the fixture has been built.
    /// </summary>
    [Fact]
    public async Task ReadsRegistrationsFromBuiltFixtureApp()
    {
        var repoRoot = FindRepositoryRoot();
        if (repoRoot is null) return;

        var appDir = Path.Combine(repoRoot, "fixtures", "SampleApp");
        if (!File.Exists(Path.Combine(appDir, "bin", "Debug", "net8.0", "SampleApp.dll"))) return;

        if (!FindDeployedProbes().TryGetValue("net8.0", out var probe)) return;

        var registry = await new AssemblyProbeSource(probe).LoadAsync(appDir, default);

        Assert.NotNull(registry);
        // cc:MyControl is registered only in DotvvmStartup.cs; it cannot be read from markup
        Assert.True(registry!.IsKnownTag("cc", "MyControl"));
        Assert.True(registry.IsKnownPrefix("dot"));

        // The whole point of tier 3: the properties come from the real assemblies. Repeater
        // lives in DotVVM.Framework, so this also proves the registered assemblies are scanned.
        var repeater = registry.GetControl("dot", "Repeater");
        Assert.NotNull(repeater);
        Assert.Contains("ItemTemplate", repeater!.Properties);
        // Inherited from the base classes; without the whole class-constructor chain it is absent
        Assert.Contains("Visible", repeater.Properties);
    }

    /// <summary>
    /// Finds the probe variants in the server project's output. The layout is
    /// probe/&lt;tfm&gt;/DotVVM.LanguageServer.Probe.dll, keyed by the TFM folder name.
    /// </summary>
    private static Dictionary<string, string> FindDeployedProbes()
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var root = FindRepositoryRoot();
        if (root is null) return result;

        var serverBin = Path.Combine(root, "server", "src", "DotVVM.LanguageServer", "bin");
        if (!Directory.Exists(serverBin)) return result;

        foreach (var file in Directory.EnumerateFiles(
                     serverBin, "DotVVM.LanguageServer.Probe.dll", SearchOption.AllDirectories))
        {
            var tfmDir = Path.GetDirectoryName(file);
            var probeDir = Path.GetDirectoryName(tfmDir);
            if (Path.GetFileName(probeDir) != "probe") continue;

            result[Path.GetFileName(tfmDir)!] = file;
        }
        return result;
    }

    /// <summary>
    /// The repository root is recognised by .git. A fixtures subdirectory cannot be used as the
    /// marker: the test data directory has the same name, so the search would stop right away in
    /// the test output directory.
    /// </summary>
    private static string? FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git"))) return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }

    [Theory]
    [InlineData("net8.0", "8.0.0")]
    [InlineData("net9.0", "9.0.0")]
    public void SelectsProbeMatchingTargetRuntime(string expectedFolder, string frameworkVersion)
    {
        var dir = Path.Combine(Path.GetTempPath(), "dotvvm-tfm-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "App.runtimeconfig.json"),
                "{\"runtimeOptions\":{\"tfm\":\"" + expectedFolder +
                "\",\"framework\":{\"name\":\"Microsoft.NETCore.App\",\"version\":\"" +
                frameworkVersion + "\"}}}");

            var tfm = AssemblyProbeSource.ReadTargetFramework(Path.Combine(dir, "App.dll"));
            Assert.Equal(expectedFolder, tfm);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void FallsBackWhenRuntimeConfigMissing()
    {
        Assert.Null(AssemblyProbeSource.ReadTargetFramework("/nonexistent/App.dll"));
    }

    /// <summary>
    /// The probe must be available for several TFMs. With a net8.0 probe alone, the assembly of
    /// a project targeting net9.0 cannot be loaded — the runtime rejects System.Runtime 9.0.0.0
    /// and tier 3 silently drops out.
    /// </summary>
    [Fact]
    public void ProbeIsDeployedForMultipleTargetFrameworks()
    {
        var found = FindDeployedProbes();

        Assert.Contains("net8.0", found.Keys);
        Assert.Contains("net9.0", found.Keys);
    }
}
