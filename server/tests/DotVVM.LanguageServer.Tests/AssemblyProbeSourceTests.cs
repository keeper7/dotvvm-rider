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
        var probe = FindDeployedProbe();
        Assert.True(probe is not null,
            "probe/DotVVM.LanguageServer.Probe.dll chybí ve výstupu serveru");
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

        var probe = FindDeployedProbe();
        if (probe is null) return;

        var registry = await new AssemblyProbeSource(probe).LoadAsync(appDir, default);

        Assert.NotNull(registry);
        // cc:MyControl je registrovaná jen v DotvvmStartup.cs — z markupu se vyčíst nedá
        Assert.True(registry!.IsKnownTag("cc", "MyControl"));
        Assert.True(registry.IsKnownPrefix("dot"));
    }

    /// <summary>Najde probe ve výstupu serverového projektu, ať už Debug nebo Release.</summary>
    private static string? FindDeployedProbe()
    {
        var root = FindRepositoryRoot();
        if (root is null) return null;

        var serverBin = Path.Combine(root, "server", "src", "DotVVM.LanguageServer", "bin");
        if (!Directory.Exists(serverBin)) return null;

        return Directory
            .EnumerateFiles(serverBin, "DotVVM.LanguageServer.Probe.dll", SearchOption.AllDirectories)
            .FirstOrDefault(f => Path.GetFileName(Path.GetDirectoryName(f)) == "probe");
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
}
