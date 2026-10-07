using GitCommands;

namespace GitExtensions.Xplat.Core.Settings;

/// <summary>
///  The ssh client choices of upstream's <c>SshSettingsPage</c>.
/// </summary>
public enum SshClientKind
{
    /// <summary>The ssh git finds itself: upstream stores an empty <c>gitssh</c>.</summary>
    OpenSsh,

    /// <summary>PuTTY's plink: <c>gitssh</c> is the plink path.</summary>
    Putty,

    /// <summary>Any other ssh program: <c>gitssh</c> is its path.</summary>
    Other,
}

/// <summary>
///  PuTTY's programs as upstream's SSH page shows them.
/// </summary>
public sealed record PuttyPaths(string Plink, string Puttygen, string Pageant);

/// <summary>
///  Upstream's SSH page logic: which client a stored <c>gitssh</c> path means, the path a choice stores, where PuTTY is
///  looked for, and the <c>GIT_SSH</c> variable git reads.
/// </summary>
public static class SshClients
{
    /// <summary>
    ///  The client a stored path means. As upstream's <c>GitSshHelpers.IsPlink</c>, a path ending in "plink.exe" (also
    ///  TortoisePlink.exe) is PuTTY.
    /// </summary>
    public static SshClientKind KindOf(string sshPath)
        => string.IsNullOrEmpty(sshPath) ? SshClientKind.OpenSsh
            : sshPath.EndsWith("plink.exe", StringComparison.CurrentCultureIgnoreCase) ? SshClientKind.Putty
            : SshClientKind.Other;

    /// <summary>
    ///  The <c>gitssh</c> value a choice stores, as upstream's page writes it.
    /// </summary>
    public static string PathFor(SshClientKind kind, string plink, string other)
        => kind switch
        {
            SshClientKind.OpenSsh => "",
            SshClientKind.Putty => plink,
            _ => other,
        };

    /// <summary>
    ///  Sets <c>GIT_SSH</c> for the git processes this app starts, as upstream does at startup and when its page is saved;
    ///  an empty path unsets it so git uses its own ssh.
    /// </summary>
    public static void Apply(string sshPath) => GitSshHelpers.SetGitSshEnvironmentVariable(sshPath);

    /// <summary>
    ///  The folders upstream's <c>GetPuttyLocations</c> looks in, in its order: <c>GITEXT_PUTTY</c>, then PuTTY, TortoiseGit
    ///  and TortoiseSvn under Program Files (also the 32-bit one on a 64-bit Windows). Upstream also asks the registry for
    ///  an old PuTTY uninstaller entry; that is not done here.
    /// </summary>
    public static IEnumerable<string> PuttyLocations(Func<string, string?> environment, bool is64Bit)
    {
        string? fromVariable = environment("GITEXT_PUTTY");
        if (!string.IsNullOrEmpty(fromVariable))
        {
            yield return fromVariable;
        }

        string? programFiles = environment("ProgramFiles");
        string? programFilesX86 = is64Bit || !string.IsNullOrEmpty(environment("PROCESSOR_ARCHITEW6432"))
            ? environment("ProgramFiles(x86)")
            : null;
        foreach (string folder in (string[])[@"PuTTY\", @"TortoiseGit\bin\", @"TortoiseSvn\bin\"])
        {
            yield return programFiles + @"\" + folder;
            if (programFilesX86 is not null)
            {
                yield return programFilesX86 + @"\" + folder;
            }
        }
    }

    /// <summary>
    ///  Upstream's <c>AutoFindPuttyPaths</c>: fills each path that names no existing file from the first location that has
    ///  the program (plink.exe, else TortoisePlink.exe; puttygen.exe; pageant.exe), stopping at the first location that
    ///  completes all three. Only on Windows, as upstream.
    /// </summary>
    public static PuttyPaths FindPutty(PuttyPaths current, IEnumerable<string> locations, Func<string, bool> fileExists)
    {
        PuttyPaths paths = current;
        foreach (string location in locations)
        {
            string folder = location.EndsWith('\\') ? location : location + @"\";
            if (!fileExists(paths.Plink))
            {
                if (fileExists(folder + "plink.exe"))
                {
                    paths = paths with { Plink = folder + "plink.exe" };
                }
                else if (fileExists(folder + "TortoisePlink.exe"))
                {
                    paths = paths with { Plink = folder + "TortoisePlink.exe" };
                }
            }

            if (!fileExists(paths.Puttygen) && fileExists(folder + "puttygen.exe"))
            {
                paths = paths with { Puttygen = folder + "puttygen.exe" };
            }

            if (!fileExists(paths.Pageant) && fileExists(folder + "pageant.exe"))
            {
                paths = paths with { Pageant = folder + "pageant.exe" };
            }

            if (fileExists(paths.Plink) && fileExists(paths.Puttygen) && fileExists(paths.Pageant))
            {
                break;
            }
        }

        return paths;
    }
}
