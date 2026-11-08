namespace GitExtensions.Xplat.Core.Platform;

/// <summary>
///  The form of a path git prints: git reports real folders, so a repository opened through a symbolic link has one path
///  in the app and another in git's output, on macOS even for the temporary folder (<c>/var</c> links to <c>/private/var</c>).
/// </summary>
public static class RealPath
{
    /// <summary>
    ///  Gets <paramref name="path"/> as a full path with every symbolic link in it followed. A part of the path that cannot
    ///  be read stays as written, since a folder may be missing. Windows paths are returned as full paths only, as upstream
    ///  compares them there.
    /// </summary>
    public static string Resolve(string path)
    {
        string fullPath = Path.GetFullPath(path);
        return OperatingSystem.IsWindows() ? fullPath : ResolveLinks(fullPath);
    }

    private static string ResolveLinks(string fullPath)
    {
        string current = Path.GetPathRoot(fullPath) ?? string.Empty;
        foreach (string part in fullPath[current.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            string next = Path.Combine(current, part);
            current = LinkTarget(next) is { } target ? ResolveLinks(target) : next;
        }

        return current;
    }

    private static string? LinkTarget(string path)
    {
        try
        {
            return new DirectoryInfo(path).ResolveLinkTarget(returnFinalTarget: true)?.FullName;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
