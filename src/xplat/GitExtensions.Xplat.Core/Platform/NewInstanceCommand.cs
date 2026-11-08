namespace GitExtensions.Xplat.Core.Platform;

/// <summary>
///  Starts the app again on a repository, as upstream's "new instance" items do. In a macOS bundle that goes through
///  <c>open -n</c>: the bundle's executable started directly has no Dock icon or application menu of its own.
/// </summary>
public static class NewInstanceCommand
{
    private const string BundleExecutableFolder = ".app/Contents/MacOS/";
    private const string BundleSuffix = ".app";

    /// <summary>
    ///  The program and arguments that open <paramref name="repositoryPath"/> in a new instance of <paramref name="processPath"/>.
    /// </summary>
    public static (string FileName, IReadOnlyList<string> Arguments) For(string processPath, string repositoryPath, bool isMacOS)
    {
        int bundleEnd = isMacOS ? processPath.LastIndexOf(BundleExecutableFolder, StringComparison.Ordinal) : -1;
        return bundleEnd < 0
            ? (processPath, [repositoryPath])
            : ("open", ["-n", processPath[..(bundleEnd + BundleSuffix.Length)], "--args", repositoryPath]);
    }

    /// <summary>
    ///  Opens <paramref name="repositoryPath"/> in a new instance of the running app.
    /// </summary>
    public static void Start(IProcessLauncher launcher, string repositoryPath)
    {
        if (Environment.ProcessPath is { } process)
        {
            (string fileName, IReadOnlyList<string> arguments) = For(process, repositoryPath, OperatingSystem.IsMacOS());
            launcher.Start(fileName, arguments, repositoryPath);
        }
    }
}

/// <summary>
///  What a folder or file dropped on the Dock icon, or given to <c>open</c>, means for the app.
/// </summary>
public static class OpenedItem
{
    /// <summary>
    ///  The folder to open as a repository: the item itself, or for a file the folder it is in.
    /// </summary>
    public static string Folder(string path, Func<string, bool> isFolder)
        => isFolder(path) ? path : Path.GetDirectoryName(path) ?? path;
}
