namespace GitExtensions.Xplat.Core.Platform;

/// <summary>
///  The program that starts this app, for commands that run it again: a new instance, a restart, git's editor and password
///  prompt, and scripts. From an AppImage that is the AppImage file, not the running executable: the executable is in a
///  folder the AppImage mounts only while its first process runs, under a new name each time, so a process started from it
///  can outlive the mount and a path stored from it (git's <c>core.editor</c>) points nowhere after the app ends.
/// </summary>
public static class AppExecutable
{
    /// <summary>
    ///  Names the variable that the AppImage runtime sets to the AppImage file's full path.
    /// </summary>
    public const string AppImageVariable = "APPIMAGE";

    /// <summary>
    ///  Names the variable that the AppImage runtime sets to the folder where it mounted the AppImage.
    /// </summary>
    public const string AppDirVariable = "APPDIR";

    /// <summary>
    ///  The program that starts the running app, or <see langword="null"/> when the process has no path.
    /// </summary>
    public static string? Current
        => For(Environment.ProcessPath, Environment.GetEnvironmentVariable(AppImageVariable),
            Environment.GetEnvironmentVariable(AppDirVariable));

    /// <summary>
    ///  The program that starts the app running from <paramref name="processPath"/>. <paramref name="appImage"/> counts only
    ///  when <paramref name="processPath"/> is inside <paramref name="appDir"/>: both variables are inherited, so an app
    ///  started from a terminal that is itself an AppImage sees that terminal's.
    /// </summary>
    public static string? For(string? processPath, string? appImage, string? appDir)
    {
        if (processPath is null || string.IsNullOrEmpty(appImage) || string.IsNullOrEmpty(appDir))
        {
            return processPath;
        }

        string folder = Path.TrimEndingDirectorySeparator(appDir) + Path.DirectorySeparatorChar;
        return processPath.StartsWith(folder, StringComparison.Ordinal) ? appImage : processPath;
    }
}
