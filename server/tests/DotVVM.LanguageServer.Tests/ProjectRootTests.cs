using DotVVM.LanguageServer.Configuration;
using Xunit;

namespace DotVVM.LanguageServer.Tests;

public class ProjectRootTests
{
    [Fact]
    public void FindsTheDirectoryHoldingTheProjectFile()
    {
        var root = Path.Combine(Path.GetTempPath(), "dotvvm-root-" + Guid.NewGuid().ToString("N"));
        var nested = Path.Combine(root, "Controls", "SocialCare");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(root, "App.csproj"), "<Project />");
        try
        {
            Assert.Equal(
                new DirectoryInfo(root).FullName,
                new DirectoryInfo(ProjectRoot.Find(nested)!).FullName);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    /// <summary>
    /// The starting directory can be gone - a file renamed or deleted while the editor still has
    /// it open. That must return null, not throw: the caller sits outside the per-source
    /// try/catch, so an exception here would take the whole configuration lookup down.
    /// </summary>
    [Fact]
    public void ReturnsNullForADirectoryThatDoesNotExist()
    {
        Assert.Null(ProjectRoot.Find(
            Path.Combine(Path.GetTempPath(), "dotvvm-missing-" + Guid.NewGuid().ToString("N"))));
    }
}
