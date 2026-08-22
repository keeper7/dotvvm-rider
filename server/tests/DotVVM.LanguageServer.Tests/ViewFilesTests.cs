using DotVVM.LanguageServer.Configuration;
using Xunit;

namespace DotVVM.LanguageServer.Tests;

public class ViewFilesTests : IDisposable
{
    private readonly List<string> _temporary = new();

    private string CreateProject(params string[] relativePaths)
    {
        var root = Path.Combine(Path.GetTempPath(), "viewfiles-" + Guid.NewGuid().ToString("N"));
        _temporary.Add(root);
        foreach (var relative in relativePaths)
        {
            var full = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, "");
        }
        return root;
    }

    public void Dispose()
    {
        foreach (var dir in _temporary.Where(Directory.Exists)) Directory.Delete(dir, true);
    }

    [Fact]
    public void FindsFilesRelativeToTheProjectRoot()
    {
        var root = CreateProject("Views/SiteMaster.dotmaster", "Views/Nested/Sub.dotmaster");

        var found = ViewFiles.Find(root, ".dotmaster");

        Assert.Equal(
            new[] { "Views/Nested/Sub.dotmaster", "Views/SiteMaster.dotmaster" },
            found.OrderBy(x => x, StringComparer.Ordinal));
    }

    [Fact]
    public void UsesForwardSlashesOnEveryPlatform()
    {
        // DotVVM reads the path from the directive the same way everywhere; a backslash
        // would not resolve
        var root = CreateProject("Views/Deep/A.dotmaster");

        Assert.All(ViewFiles.Find(root, ".dotmaster"), p => Assert.DoesNotContain('\\', p));
    }

    [Fact]
    public void SkipsBinAndObj()
    {
        // A published copy under bin/ is not a path anyone would want to write
        var root = CreateProject("Views/A.dotmaster", "bin/Debug/net9.0/Views/A.dotmaster",
                                 "obj/Debug/A.dotmaster");

        Assert.Single(ViewFiles.Find(root, ".dotmaster"));
    }

    [Fact]
    public void SkipsHiddenDirectories()
    {
        var root = CreateProject("Views/A.dotmaster", ".git/objects/A.dotmaster");

        Assert.Single(ViewFiles.Find(root, ".dotmaster"));
    }

    [Fact]
    public void ReturnsNothingForADirectoryThatIsNotThere()
    {
        // The server is told about a file the editor has open, and it can be gone by now.
        // ProjectRoot.Find had exactly this hole once.
        Assert.Empty(ViewFiles.Find("/no/such/directory/at/all", ".dotmaster"));
    }

    [Fact]
    public void MatchesTheExtensionCaseInsensitively()
    {
        var root = CreateProject("Views/A.DotMaster");
        Assert.Single(ViewFiles.Find(root, ".dotmaster"));
    }
}
