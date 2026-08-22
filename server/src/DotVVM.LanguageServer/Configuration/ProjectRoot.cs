namespace DotVVM.LanguageServer.Configuration;

/// <summary>
/// Finds the project root: the nearest directory upwards that holds a .csproj. The server is
/// told only which file the user has open, so everything relative to the project - the Src of a
/// markup control, the built assembly - starts here.
/// </summary>
public static class ProjectRoot
{
    public static string? Find(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir is not null)
        {
            // A directory that is not there is walked past, not thrown over: the server is told
            // about a file the editor has open, and it can be deleted or renamed under us.
            if (dir.Exists && dir.EnumerateFiles("*.csproj").Any()) return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }
}
