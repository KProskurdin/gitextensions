using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.IsolatedStorage;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace GitExtensions.Xplat.Core.BuildServer;

/// <summary>
///  Where the build server credentials are kept, by the adapter's unique key (its server). The text is upstream's: a git
///  config file with a "Credentials" section.
/// </summary>
public interface IBuildServerCredentialStore
{
    /// <summary>
    ///  The stored text, or null when there is none or it cannot be read (e.g. stored by another Windows user).
    /// </summary>
    string? Load(string uniqueKey);

    void Save(string uniqueKey, string text);
}

/// <summary>
///  Picks the store for the OS: upstream's on Windows, the Secret Service on Linux when <c>secret-tool</c> is installed, and
///  otherwise the session's memory, so a secret is never written in plain text.
/// </summary>
public static class BuildServerCredentialStores
{
    public static IBuildServerCredentialStore ForCurrentOs()
    {
        if (OperatingSystem.IsWindows())
        {
            return new UpstreamBuildServerCredentialStore();
        }

        if (OperatingSystem.IsLinux() && SecretToolCredentialStore.IsAvailable())
        {
            return new SecretToolCredentialStore();
        }

        return new SessionBuildServerCredentialStore();
    }
}

/// <summary>
///  Upstream's store: the user's isolated storage, one "BuildServer-&lt;base64 key&gt;.options" file per server, encrypted for
///  the Windows user with DPAPI, as upstream's <c>BuildServerWatcher</c> reads and writes it.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class UpstreamBuildServerCredentialStore : IBuildServerCredentialStore
{
    public string? Load(string uniqueKey)
    {
        using IsolatedStorageFileStream stream = Open(uniqueKey, FileAccess.Read, FileShare.Read);
        if (stream.Position >= stream.Length)
        {
            return null;
        }

        byte[] protectedData = new byte[stream.Length];
        stream.ReadExactly(protectedData, 0, (int)stream.Length);
        try
        {
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(protectedData, null, DataProtectionScope.CurrentUser));
        }
        catch (CryptographicException)
        {
            // As upstream: data protected by another Windows user cannot be read, so the user enters the credentials again.
            return null;
        }
    }

    public void Save(string uniqueKey, string text)
    {
        byte[] protectedData = ProtectedData.Protect(Encoding.UTF8.GetBytes(text), null, DataProtectionScope.CurrentUser);
        using IsolatedStorageFileStream stream = Open(uniqueKey, FileAccess.Write, FileShare.None);
        stream.SetLength(0);
        stream.Write(protectedData, 0, protectedData.Length);
    }

    private static IsolatedStorageFileStream Open(string uniqueKey, FileAccess access, FileShare share)
        => new(string.Format("BuildServer-{0}.options", Convert.ToBase64String(Encoding.UTF8.GetBytes(uniqueKey))),
            FileMode.OpenOrCreate, access, share);
}

/// <summary>
///  The desktop's Secret Service (GNOME Keyring, KWallet) through libsecret's <c>secret-tool</c>, which takes the secret on
///  its standard input, so it never appears on a command line.
/// </summary>
public sealed class SecretToolCredentialStore : IBuildServerCredentialStore
{
    private const string Program = "secret-tool";
    private const string Service = "git-extensions-build-server";

    /// <summary>
    ///  True when <c>secret-tool</c> is on the PATH.
    /// </summary>
    public static bool IsAvailable()
        => (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Any(folder => File.Exists(Path.Join(folder, Program)));

    public string? Load(string uniqueKey)
    {
        (int exitCode, string output) = Run(["lookup", "service", Service, "server", uniqueKey], input: null);
        return exitCode == 0 && output.Length > 0 ? output : null;
    }

    public void Save(string uniqueKey, string text)
        => Run(["store", $"--label=Git Extensions build server {uniqueKey}", "service", Service, "server", uniqueKey], text);

    private static (int ExitCode, string Output) Run(IReadOnlyList<string> arguments, string? input)
    {
        ProcessStartInfo startInfo = new(Program)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            using Process process = Process.Start(startInfo)!;
            if (input is not null)
            {
                process.StandardInput.Write(input);
            }

            process.StandardInput.Close();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return (process.ExitCode, output);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or InvalidOperationException)
        {
            return (-1, "");
        }
    }
}

/// <summary>
///  Keeps the credentials for this run of the app only; the user enters them again after a restart. Tests use it too.
/// </summary>
public sealed class SessionBuildServerCredentialStore : IBuildServerCredentialStore
{
    private readonly ConcurrentDictionary<string, string> _texts = [];

    public string? Load(string uniqueKey) => _texts.GetValueOrDefault(uniqueKey);

    public void Save(string uniqueKey, string text) => _texts[uniqueKey] = text;
}
