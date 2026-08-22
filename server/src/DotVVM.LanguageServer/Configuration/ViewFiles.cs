namespace DotVVM.LanguageServer.Configuration;

/// <summary>
/// Lists the project's view files by extension, as paths relative to its root - which is the
/// shape a directive wants: `@masterPage Views/SiteMaster.dotmaster`.
/// </summary>
public static class ViewFiles
{
    /// <summary>Directories that hold copies, not sources.</summary>
    private static readonly string[] Skipped = ["bin", "obj", "node_modules"];

    public static IReadOnlyList<string> Find(string root, string extension)
    {
        var directory = new DirectoryInfo(root);
        if (!directory.Exists) return [];

        var result = new List<string>();
        Collect(directory, directory.FullName, extension, result);
        return result;
    }

    private static void Collect(
        DirectoryInfo directory, string root, string extension, List<string> result)
    {
        foreach (var file in Enumerate(directory))
        {
            if (!file.Name.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) continue;
            result.Add(Path.GetRelativePath(root, file.FullName).Replace('\\', '/'));
        }

        foreach (var child in EnumerateDirectories(directory))
        {
            if (child.Name.StartsWith('.')) continue;
            if (Skipped.Contains(child.Name, StringComparer.OrdinalIgnoreCase)) continue;
            Collect(child, root, extension, result);
        }
    }

    /// <summary>
    /// A directory can be removed or made unreadable while this walks it; either way the rest
    /// of the tree is still worth listing.
    /// </summary>
    private static IEnumerable<FileInfo> Enumerate(DirectoryInfo directory)
    {
        try { return directory.EnumerateFiles(); }
        catch (IOException) { return []; }
        catch (UnauthorizedAccessException) { return []; }
    }

    private static IEnumerable<DirectoryInfo> EnumerateDirectories(DirectoryInfo directory)
    {
        try { return directory.EnumerateDirectories(); }
        catch (IOException) { return []; }
        catch (UnauthorizedAccessException) { return []; }
    }
}
