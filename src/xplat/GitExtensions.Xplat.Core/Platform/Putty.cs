using System.Diagnostics;
using System.Text;
using GitCommands;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;

namespace GitExtensions.Xplat.Core.Platform;

/// <summary>
///  Upstream's PuTTY helpers without their windows (<c>PuttyHelpers.StartPageantIfConfigured</c>, <c>Plink</c>): loading a
///  key into Pageant and testing a connection with plink. They apply when git uses plink (Settings > SSH), which upstream
///  supports on Windows only.
/// </summary>
public static class Putty
{
    // Upstream's TranslatedStrings.
    public const string KeyNotFound = "SSH key file could not be found";
    public const string NotInstalled = "Is PuTTY installed?";
    public const string NotConfigured = "PuTTY is not configured as SSH client";
    public const string WhereConfigure = "SSH client can be configured in Settings > SSH.";

    // Upstream's Plink runs plink in a console window that stays open (cmd.exe /k) so the user sees the answer.
    private const string ConsoleProgram = "cmd.exe";

    /// <summary>
    ///  Upstream's <c>StartPageantIfConfigured</c>: Pageant started when it is not running, then given the key. Returns
    ///  upstream's error (with its explanation on a second line) or <see langword="null"/>; no key is no error.
    /// </summary>
    /// <param name="translate">Translates upstream's texts.</param>
    public static string? StartPageantIfConfigured(string? sshKeyFile, Func<string, string> translate)
    {
        if (!GitSshHelpers.IsPlink)
        {
            return translate(NotConfigured) + Environment.NewLine + translate(WhereConfigure);
        }

        string pageant = AppSettings.Pageant;
        if (!File.Exists(pageant))
        {
            return $"'{pageant}'{Environment.NewLine}{Environment.NewLine}{translate(NotInstalled)}";
        }

        Executable pageantExecutable = new(pageant);

        // As upstream: Pageant must be running before it is given a key, or the call waits until Pageant exits.
        if (!IsRunning(pageant))
        {
            IProcess process = pageantExecutable.Start();
            process.WaitForInputIdle();
        }

        if (string.IsNullOrWhiteSpace(sshKeyFile))
        {
            return null;
        }

        if (!File.Exists(sshKeyFile))
        {
            return translate(KeyNotFound) + Environment.NewLine + $"'{sshKeyFile}'";
        }

        pageantExecutable.RunCommand(sshKeyFile.Quote(), throwOnErrorExit: false);
        return null;
    }

    /// <summary>
    ///  Upstream's <c>FormPull.LoadPuttyKey</c> and <c>FormPush.StartPageant</c>: with plink, each distinct PuTTY key of
    ///  <paramref name="remotes"/> (upstream's <c>GetPuttyKeyFileForRemote</c>) given to Pageant. Returns the first error,
    ///  or <see langword="null"/>; without plink nothing is done.
    /// </summary>
    public static string? LoadKeys(IGitModule module, IReadOnlyList<string> remotes, Func<string, string> translate)
    {
        if (!GitSshHelpers.IsPlink)
        {
            return null;
        }

        HashSet<string> files = new(new PathEqualityComparer());
        foreach (string remote in remotes)
        {
            string keyFile = module.GetPuttyKeyFileForRemote(remote);
            if (!string.IsNullOrEmpty(keyFile))
            {
                files.Add(keyFile);
            }
        }

        foreach (string keyFile in files)
        {
            if (StartPageantIfConfigured(keyFile, translate) is { } error)
            {
                return error;
            }
        }

        return null;
    }

    /// <summary>
    ///  Upstream's <c>Plink.ConnectAsync</c>: plink connects to the URL's host in a console window that stays open. Returns
    ///  upstream's error when plink is not where the settings say, else <see langword="null"/> once the window is closed.
    /// </summary>
    public static async Task<string?> TestConnectionAsync(string url, Func<string, string> translate)
    {
        string plink = AppSettings.Plink;
        if (!File.Exists(plink))
        {
            return $"'{plink}'{Environment.NewLine}{Environment.NewLine}{translate(NotInstalled)}";
        }

        string arguments = $"/k \"\"{plink}\" -T {PlinkCompatibleUrl(url)}\"";
        using IProcess process = new Executable(ConsoleProgram).Start(arguments, createWindow: true,
            redirectInput: false, redirectOutput: false, outputEncoding: null);
        await process.WaitForExitAsync();
        return null;
    }

    /// <summary>
    ///  Upstream's <c>GetPlinkCompatibleUrl</c>: an ssh:// URL as plink takes it best (<c>-P port "user@host:path"</c>),
    ///  any other URL quoted.
    /// </summary>
    public static string PlinkCompatibleUrl(string inputUrl)
    {
        if (!inputUrl.StartsWith("ssh", StringComparison.Ordinal) || !Uri.IsWellFormedUriString(inputUrl, UriKind.Absolute))
        {
            return inputUrl.Quote();
        }

        Uri uri = new(inputUrl, UriKind.Absolute);
        StringBuilder fixedUrl = new();
        if (!uri.IsDefaultPort)
        {
            fixedUrl.Append("-P ").Append(uri.Port).Append(' ');
        }

        fixedUrl.Append('"');
        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            fixedUrl.Append(uri.UserInfo).Append('@');
        }

        fixedUrl.Append(uri.Host).Append(':').Append(uri.LocalPath, 1, uri.LocalPath.Length - 1).Append('"');
        return fixedUrl.ToString();
    }

    private static bool IsRunning(string program)
    {
        Process[] processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(program));
        bool running = processes.Length != 0;
        foreach (Process process in processes)
        {
            process.Dispose();
        }

        return running;
    }
}
