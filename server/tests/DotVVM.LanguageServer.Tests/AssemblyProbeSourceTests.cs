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
        // Zdroj nesmí vyhodit výjimku, když probe není k dispozici
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
    public void ReturnsNullForMalformedProbeOutput()
    {
        Assert.Null(AssemblyProbeSource.ParseProbeOutput("{ broken"));
    }

    /// <summary>
    /// Probe se musí sestavit do podadresáře probe/ ve výstupu serveru — tam ho
    /// za běhu hledá DefaultProbePath() přes AppContext.BaseDirectory. Kdyby se
    /// přestal kopírovat, LoadAsync by mlčky vracel null a stupeň 3 by nikdy
    /// nenaběhl. Kontroluje se výstup serveru, ne testů: do output adresáře testů
    /// se kopíruje jen serverová dll, podadresář probe/ už ne.
    /// </summary>
    [Fact]
    public void ProbeIsDeployedIntoServerOutput()
    {
        Assert.NotEmpty(FindDeployedProbes());
    }

    /// <summary>
    /// Ostré ověření celého stupně 3: spustí probe proti sestavené fixture
    /// aplikaci. Přeskočí se, dokud fixture není sestavená.
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
        // cc:MyControl je registrovaná jen v DotvvmStartup.cs — z markupu se vyčíst nedá
        Assert.True(registry!.IsKnownTag("cc", "MyControl"));
        Assert.True(registry.IsKnownPrefix("dot"));
    }

    /// <summary>
    /// Najde varianty probe ve výstupu serverového projektu. Layout je
    /// probe/&lt;tfm&gt;/DotVVM.LanguageServer.Probe.dll — klíčem je název TFM složky.
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
    /// Kořen repozitáře poznáme podle .git. Podle podadresáře fixtures se řídit nelze —
    /// stejný název má i adresář s testovacími daty, takže by hledání skončilo hned
    /// v output adresáři testů.
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
    /// Probe musí být k dispozici pro víc TFM. S jediným net8.0 probem nelze načíst
    /// assembly projektu cíleného na net9.0 — runtime odmítne System.Runtime 9.0.0.0
    /// a stupeň 3 mlčky vypadne.
    /// </summary>
    [Fact]
    public void ProbeIsDeployedForMultipleTargetFrameworks()
    {
        var found = FindDeployedProbes();

        Assert.Contains("net8.0", found.Keys);
        Assert.Contains("net9.0", found.Keys);
    }
}
