using System.Runtime.Versioning;
using GitCommands;
using Microsoft.Win32;

namespace GitExtensions.Xplat.Core.Platform;

/// <summary>
///  What finding git and its linux tools looks at: the environment, the file system, the Git for Windows uninstaller's
///  registry entry and git itself. <see cref="System"/> is this Windows.
/// </summary>
public sealed record WindowsGitLookup
{
    public required Func<string, string?> Environment { get; init; }

    public required Func<string, bool> FileExists { get; init; }

    public required Func<string, bool> DirectoryExists { get; init; }

    /// <summary>
    ///  Git for Windows' install folder from its uninstaller (<c>Git_is1</c>'s <c>InstallLocation</c>), or null.
    /// </summary>
    public required Func<string?> InstalledGit { get; init; }

    /// <summary>
    ///  The local application data folder (<c>%LOCALAPPDATA%</c>), where a per-user Git for Windows goes.
    /// </summary>
    public required string LocalApplicationData { get; init; }

    public required bool Is64BitProcess { get; init; }

    /// <summary>
    ///  True when <c>git --version</c> run with the command prints something, as upstream's <c>TestGitCommand</c> asks.
    /// </summary>
    public required Func<string, bool> CanRun { get; init; }

    [SupportedOSPlatform("windows")]
    public static WindowsGitLookup System { get; } = new()
    {
        Environment = global::System.Environment.GetEnvironmentVariable,
        FileExists = File.Exists,
        DirectoryExists = Directory.Exists,
        InstalledGit = () =>
        {
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Git_is1", writable: false);
            return key?.GetValue("InstallLocation") as string;
        },
        LocalApplicationData = global::System.Environment.GetFolderPath(
            global::System.Environment.SpecialFolder.LocalApplicationData),
        Is64BitProcess = global::System.Environment.Is64BitProcess,
        CanRun = command =>
        {
            try
            {
                return !string.IsNullOrEmpty(new Executable(command).GetOutput("--version"));
            }
            catch (Exception)
            {
                // Upstream ignores it: this is a search for a git that runs.
                return false;
            }
        },
    };
}

/// <summary>
///  Upstream's <c>CheckSettingsLogic.SolveGitCommand</c> and <c>SolveLinuxToolsDir</c> on Windows: the git command and the
///  folder with <c>sh</c>, looked for where Git for Windows (or msysgit, or cygwin) is usually installed.
/// </summary>
public static class WindowsGit
{
    // Upstream's texts (ChecklistSettingsPage) for its repairs of the git and linux tools rows.
    public const string LocateGitCaption = "Locate git";
    public const string GitCanBeRun = "Git can be run using: {0}";
    public const string SolveGitCommandFailed1 = "The command to run git could not be determined automatically.";

    public const string SolveGitCommandFailed2 =
        "Please make sure that Git for Windows is installed or set the correct command manually.";

    public const string LocateLinuxToolsCaption = "Locate linux tools";
    public const string ShCanBeRun = "Command sh can be run using: {0}sh";
    public const string LinuxToolsNotFound1 = "The path to linux tools (sh) could not be found automatically.";

    public const string LinuxToolsNotFound2 =
        "Please make sure there are linux tools installed (through Git for Windows or cygwin) or set the correct path manually.";

    public const string PuttyCaption = "PuTTY";
    public const string PuttyFoundAuto = "All paths needed for PuTTY could be automatically found and are set.";

    private const char Backslash = '\\';

    public static string SolveGitCommandFailed => SolveGitCommandFailed1 + Environment.NewLine + SolveGitCommandFailed2;

    public static string LinuxToolsNotFound => LinuxToolsNotFound1 + Environment.NewLine + LinuxToolsNotFound2;

    /// <summary>
    ///  Upstream's <c>GetGitLocations</c>, in its order: <c>GITEXT_GIT</c>, the uninstaller's folder, Git and msysgit under
    ///  Program Files (also the 32-bit one on a 64-bit Windows), <c>C:\msysgit</c>, <c>C:\cygwin</c> and the per-user Git.
    /// </summary>
    public static IEnumerable<string> GitLocations(WindowsGitLookup lookup)
    {
        if (lookup.Environment("GITEXT_GIT") is { Length: > 0 } fromVariable)
        {
            yield return fromVariable;
        }

        // Upstream yields the uninstaller's value even when it is missing; nothing is found there then.
        if (lookup.InstalledGit() is { Length: > 0 } installed)
        {
            yield return installed;
        }

        string? programFiles = lookup.Environment("ProgramFiles");
        string? programFilesX86 =
            lookup.Is64BitProcess || !string.IsNullOrEmpty(lookup.Environment("PROCESSOR_ARCHITEW6432"))
                ? lookup.Environment("ProgramFiles(x86)")
                : null;
        foreach (string folder in (string[])[@"\Git\", @"\msysgit\"])
        {
            if (programFiles is not null)
            {
                yield return programFiles + folder;
            }

            if (programFilesX86 is not null)
            {
                yield return programFilesX86 + folder;
            }
        }

        yield return @"C:\msysgit\";
        yield return @"C:\cygwin\";
        yield return lookup.LocalApplicationData.TrimEnd(Backslash) + @"\Programs\Git\";
    }

    /// <summary>
    ///  Upstream's <c>SolveGitCommand</c> on Windows: the first command that runs of the one typed, the stored one, each
    ///  location's <c>bin\git.exe</c>, then its <c>cmd\git.exe</c> and <c>cmd\git.cmd</c>, then <c>git</c> and
    ///  <c>git.cmd</c> on the PATH; null when none runs.
    /// </summary>
    public static string? SolveGitCommand(string? stored, string? possibleNewPath, WindowsGitLookup lookup)
        => CommandCandidates(stored, possibleNewPath, lookup).FirstOrDefault(lookup.CanRun);

    /// <summary>
    ///  Upstream's <c>SolveLinuxToolsDir</c> on Windows: the folder with <c>sh</c> next to the git command
    ///  (<c>usr\bin\</c>, then <c>bin\</c>), else empty when <c>sh</c> is on the PATH (the stored folder kept while it has
    ///  <c>sh</c>); null when neither.
    /// </summary>
    /// <remarks>
    ///  Upstream also loops over its git locations there, but checks the git command's path instead of each location's,
    ///  so that loop never finds anything; it is left out.
    /// </remarks>
    public static string? SolveLinuxToolsDir(string gitCommand, string storedLinuxToolsDir, WindowsGitLookup lookup)
    {
        foreach (string toolsPath in (string[])[@"usr\bin\", @"bin\"])
        {
            string linuxToolsPath = gitCommand.Replace(@"\cmd\git.exe", @"\" + toolsPath)
                .Replace(@"\cmd\git.cmd", @"\" + toolsPath)
                .Replace(@"\bin\git.exe", @"\" + toolsPath);
            if (ContainsSh(linuxToolsPath))
            {
                return linuxToolsPath;
            }

            if (IsOnPath("sh.exe") || IsOnPath("sh"))
            {
                return ContainsSh(storedLinuxToolsDir) ? storedLinuxToolsDir : "";
            }
        }

        return null;

        bool ContainsSh(string path)
            => lookup.DirectoryExists(path) && (lookup.FileExists(path + "sh.exe") || lookup.FileExists(path + "sh"));

        // Upstream's CheckIfFileIsInPath, with Windows' separators wherever the rules run (tests run them on every
        // system); upstream also takes a file of that name in the current folder.
        bool IsOnPath(string fileName)
            => (lookup.Environment("PATH") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Any(folder => lookup.FileExists(folder.Trim().TrimEnd(Backslash) + Backslash + fileName));
    }

    // Upstream's GetWindowsCommandLocations.
    private static IEnumerable<string> CommandCandidates(string? stored, string? possibleNewPath,
        WindowsGitLookup lookup)
    {
        if (!string.IsNullOrEmpty(possibleNewPath) && lookup.FileExists(possibleNewPath))
        {
            yield return possibleNewPath;
        }

        if (!string.IsNullOrEmpty(stored) && lookup.FileExists(stored))
        {
            yield return stored;
        }

        List<string> locations = [.. GitLocations(lookup)];
        foreach (string path in locations.Where(path => lookup.DirectoryExists(path + @"bin\")))
        {
            yield return path + @"bin\git.exe";
        }

        foreach (string path in locations.Where(path => lookup.DirectoryExists(path + @"cmd\")))
        {
            yield return path + @"cmd\git.exe";
            yield return path + @"cmd\git.cmd";
        }

        yield return "git";
        yield return "git.cmd";
    }
}
