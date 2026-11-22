using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using GitCommands;
using Microsoft.Win32;

namespace GitExtensions.Xplat.Core.Platform;

/// <summary>
///  What upstream's Windows installer registers and its settings check: the install folder in
///  <c>[Software\GitExtensions\InstallDir]</c>, which the Explorer shell extension starts <c>GitExtensions.exe</c> from, and
///  the shell extension itself (upstream's <c>ShellExtensionManager</c>).
/// </summary>
public interface IWindowsInstallation
{
    /// <summary>
    ///  Upstream's <c>AppSettings.GetInstallDir</c>: the stored install folder, or the app's own when none is stored.
    /// </summary>
    string? InstallDir { get; }

    /// <summary>
    ///  The app's own folder (upstream's <c>AppSettings.GetGitExtensionsDirectory</c>).
    /// </summary>
    string? AppDirectory { get; }

    /// <summary>
    ///  Upstream's <c>CheckSettingsLogic.SolveGitExtensionsDir</c>: stores the app's own folder as the install folder; false
    ///  when that folder does not exist.
    /// </summary>
    bool SolveInstallDir();

    /// <summary>
    ///  Upstream's <c>ShellExtensionManager.FilesExist</c>: the shell extension's files are in the install folder or the
    ///  app's (the 64-bit one too on a 64-bit Windows).
    /// </summary>
    bool ShellExtensionFilesExist();

    /// <summary>
    ///  Upstream's <c>ShellExtensionManager.IsRegistered</c>: Windows approves the extension and has its three context menu
    ///  handlers.
    /// </summary>
    bool IsShellExtensionRegistered();

    /// <summary>
    ///  Upstream's <c>ShellExtensionManager.Register</c>: <c>regsvr32</c> as administrator for each of the extension's files.
    /// </summary>
    void RegisterShellExtension();

    /// <summary>
    ///  Upstream's <c>ShellExtensionManager.Unregister</c>.
    /// </summary>
    void UnregisterShellExtension();
}

/// <summary>
///  The shell extension's files, and which of them registering uses.
/// </summary>
public static class ShellExtensionFiles
{
    /// <summary>
    ///  Upstream's <c>ShellExtensionManager.GitExtensionsShellEx32Name</c>, which the Checklist names.
    /// </summary>
    public const string ShellEx32Name = "GitExtensionsShellEx32.dll";

    public const string ShellEx64Name = "GitExtensionsShellEx64.dll";

    /// <summary>
    ///  Upstream's <c>FindFileInBinFolders</c>: the file in the install folder, else in the app's; null when in neither.
    /// </summary>
    public static string? Find(string fileName, string? installDir, string? appDirectory, Func<string, bool> fileExists)
        => new[] { installDir, appDirectory }
            .Where(folder => !string.IsNullOrEmpty(folder))
            .Select(folder => Path.Join(folder, fileName))
            .FirstOrDefault(fileExists);

    /// <summary>
    ///  Upstream's <c>FilesExist</c>: the 32-bit file, and on a 64-bit Windows the 64-bit one too.
    /// </summary>
    public static bool Exist(string? installDir, string? appDirectory, bool is64BitWindows, Func<string, bool> fileExists)
        => Find(ShellEx32Name, installDir, appDirectory, fileExists) is not null &&
           (!is64BitWindows || Find(ShellEx64Name, installDir, appDirectory, fileExists) is not null);

    /// <summary>
    ///  The files to register, 64-bit first as upstream, and whether the app's folder becomes the install folder.
    /// </summary>
    /// <remarks>
    ///  Upstream stores its own folder as the install folder first and then registers the files found there, failing when
    ///  they are not. This app is often not where the installer put Git Extensions, so it does that only when the files are
    ///  in its own folder; otherwise it registers the installed files and leaves the install folder as it is, so the
    ///  extension keeps starting the <c>GitExtensions.exe</c> next to them.
    /// </remarks>
    /// <exception cref="FileNotFoundException">A file is in neither folder.</exception>
    public static (IReadOnlyList<string> Files, bool SetInstallDir) ToRegister(string? installDir, string? appDirectory,
        bool is64BitWindows, Func<string, bool> fileExists)
    {
        string[] names = is64BitWindows ? [ShellEx64Name, ShellEx32Name] : [ShellEx32Name];
        if (!string.IsNullOrEmpty(appDirectory) && names.All(name => fileExists(Path.Join(appDirectory, name))))
        {
            return ([.. names.Select(name => Path.Join(appDirectory, name))], true);
        }

        List<string> files = [];
        foreach (string name in names)
        {
            files.Add(Find(name, installDir, appDirectory, fileExists) ?? throw new FileNotFoundException(null, name));
        }

        return (files, false);
    }
}

/// <summary>
///  The install folder and shell extension of this Windows (registry, files and <c>regsvr32</c>).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsInstallation : IWindowsInstallation
{
    // Upstream's IsRegistered keys: the extension's class id and its context menu handlers.
    private const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Shell Extensions\Approved";
    private const string ShellExClassId = "{3C16B20A-BA16-4156-916F-0A375ECFFE24}";
    private const string HandlerName = "GitExtensions2";

    // The Win32 error of a declined elevation prompt.
    private const int ErrorCancelled = 1223;

    private static readonly string[] _handlerKeys =
    [
        $@"*\shellex\ContextMenuHandlers\{HandlerName}",
        $@"Directory\shellex\ContextMenuHandlers\{HandlerName}",
        $@"Directory\Background\shellex\ContextMenuHandlers\{HandlerName}",
    ];

    public string? InstallDir => AppSettings.GetInstallDir();

    public string? AppDirectory => AppSettings.GetGitExtensionsDirectory();

    public bool SolveInstallDir()
    {
        if (AppDirectory is { } folder && Directory.Exists(folder))
        {
            AppSettings.SetInstallDir(folder);
            return true;
        }

        return false;
    }

    public bool ShellExtensionFilesExist()
        => ShellExtensionFiles.Exist(InstallDir, AppDirectory, Environment.Is64BitOperatingSystem, File.Exists);

    public bool IsShellExtensionRegistered()
        => HasValue(Registry.LocalMachine, ApprovedKey, ShellExClassId) &&
           _handlerKeys.All(key => HasValue(Registry.ClassesRoot, key, valueName: null));

    public void RegisterShellExtension()
    {
        (IReadOnlyList<string> files, bool setInstallDir) = ShellExtensionFiles.ToRegister(InstallDir, AppDirectory,
            Environment.Is64BitOperatingSystem, File.Exists);
        if (setInstallDir)
        {
            SolveInstallDir();
        }

        foreach (string file in files)
        {
            RunRegSvr($"/s \"{file}\"");
        }
    }

    public void UnregisterShellExtension()
    {
        string[] names = Environment.Is64BitOperatingSystem
            ? [ShellExtensionFiles.ShellEx64Name, ShellExtensionFiles.ShellEx32Name]
            : [ShellExtensionFiles.ShellEx32Name];
        foreach (string name in names)
        {
            string file = ShellExtensionFiles.Find(name, InstallDir, AppDirectory, File.Exists)
                          ?? throw new FileNotFoundException(null, name);
            RunRegSvr($"/s /u \"{file}\"");
        }
    }

    // Upstream's CommonLogic.GetRegistryValue: a non-empty value.
    private static bool HasValue(RegistryKey root, string path, string? valueName)
    {
        using RegistryKey? key = root.OpenSubKey(path, writable: false);
        return key?.GetValue(valueName) is { } value && !string.IsNullOrEmpty(value.ToString());
    }

    private static void RunRegSvr(string arguments)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = "regsvr32",
            Arguments = arguments,
            Verb = "RunAs",
            UseShellExecute = true,
        };

        try
        {
            using Process? process = Process.Start(startInfo);
            process?.WaitForExit();
            if (process?.ExitCode is not 0)
            {
                throw new InvalidOperationException(
                    $"{startInfo.FileName} {startInfo.Arguments} exited with code {process?.ExitCode}.");
            }
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            throw new OperationCanceledException(ex.Message, ex);
        }
    }
}

/// <summary>
///  An installation kept in memory, for the systems other than Windows (nothing installed) and for tests.
/// </summary>
public sealed class InMemoryWindowsInstallation : IWindowsInstallation
{
    public string? InstallDir { get; set; }

    public string? AppDirectory { get; set; }

    /// <summary>
    ///  Whether the app's folder exists, which <see cref="SolveInstallDir"/> needs.
    /// </summary>
    public bool AppDirectoryExists { get; set; } = true;

    public bool FilesExist { get; set; }

    public bool IsRegistered { get; set; }

    /// <summary>
    ///  The error registering or unregistering throws, when set.
    /// </summary>
    public Exception? RegisterError { get; set; }

    /// <summary>
    ///  How many times the extension was registered and unregistered.
    /// </summary>
    public int Registrations { get; private set; }

    public int Unregistrations { get; private set; }

    public bool SolveInstallDir()
    {
        if (AppDirectory is null || !AppDirectoryExists)
        {
            return false;
        }

        InstallDir = AppDirectory;
        return true;
    }

    public bool ShellExtensionFilesExist() => FilesExist;

    public bool IsShellExtensionRegistered() => IsRegistered;

    public void RegisterShellExtension()
    {
        Registrations++;
        if (RegisterError is { } error)
        {
            throw error;
        }

        IsRegistered = true;
    }

    public void UnregisterShellExtension()
    {
        Unregistrations++;
        if (RegisterError is { } error)
        {
            throw error;
        }

        IsRegistered = false;
    }
}
