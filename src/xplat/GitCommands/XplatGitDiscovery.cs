using System.Diagnostics;
using GitCommands.Git;

namespace GitCommands;

/// <summary>
///  Finds a git executable off Windows. Upstream only knows the <c>gitcommand</c> setting, so the cross-platform shell
///  calls this once at startup and stores the result there. Candidates are plain POSIX paths, so this is not meant for Windows.
/// </summary>
public static class XplatGitDiscovery
{
    private const string MacCommandLineToolsGit = "/Library/Developer/CommandLineTools/usr/bin/git";
    private const string MacXcodeGit = "/Applications/Xcode.app/Contents/Developer/usr/bin/git";
    private const string MacSystemGit = "/usr/bin/git";

    private static readonly string[] _knownLocations =
    [
        MacCommandLineToolsGit,
        MacXcodeGit,
        "/usr/local/bin/git",
        "/opt/homebrew/bin/git",
        "/opt/local/bin/git",
        "/home/linuxbrew/.linuxbrew/bin/git",
        MacSystemGit,
    ];

    /// <summary>
    ///  Uses <paramref name="configuredCommand"/> when set and never falls back from it, so a wrong path is reported instead of hidden.
    /// </summary>
    public static GitDiscoveryResult Discover(string configuredCommand)
    {
        if (!string.IsNullOrWhiteSpace(configuredCommand))
        {
            return Probe(configuredCommand, GetVersionOutput);
        }

        GitDiscoveryResult? tooOld = null;
        foreach (string candidate in GetCandidates(OperatingSystem.IsMacOS(), File.Exists, Environment.GetEnvironmentVariable("PATH")))
        {
            GitDiscoveryResult result = Probe(candidate, GetVersionOutput);
            if (result.Status == GitDiscoveryStatus.Found)
            {
                return result;
            }

            tooOld ??= result.Status == GitDiscoveryStatus.TooOld ? result : null;
        }

        return tooOld ?? new GitDiscoveryResult(GitDiscoveryStatus.NotFound, Command: null, Version: null);
    }

    internal static IReadOnlyList<string> GetCandidates(bool isMacOS, Func<string, bool> fileExists, string? pathVariable)
    {
        // Without Command Line Tools or Xcode, /usr/bin/git on macOS is a stub that asks to install them.
        bool macToolsInstalled = isMacOS && (fileExists(MacCommandLineToolsGit) || fileExists(MacXcodeGit));

        List<string> candidates = [];
        foreach (string directory in (pathVariable ?? string.Empty).Split(':', StringSplitOptions.RemoveEmptyEntries))
        {
            candidates.Add($"{directory.TrimEnd('/')}/git");
        }

        candidates.AddRange(_knownLocations);

        return candidates
            .Distinct()
            .Where(candidate => fileExists(candidate) && (!isMacOS || candidate != MacSystemGit || macToolsInstalled))
            .ToList();
    }

    internal static GitDiscoveryResult Probe(string command, Func<string, string> getVersionOutput)
    {
        string output;
        try
        {
            output = getVersionOutput(command);
        }
        catch (Exception exception)
        {
            Trace.WriteLine(exception);
            return new GitDiscoveryResult(GitDiscoveryStatus.NotFound, command, Version: null);
        }

        if (!output.StartsWith("git version", StringComparison.Ordinal))
        {
            return new GitDiscoveryResult(GitDiscoveryStatus.NotFound, command, Version: null);
        }

        GitVersion version = new(output);

        // Same minimum as the settings page turns red below.
        GitDiscoveryStatus status = version < GitVersion.LastSupportedVersion ? GitDiscoveryStatus.TooOld : GitDiscoveryStatus.Found;
        return new GitDiscoveryResult(status, command, version);
    }

    private static string GetVersionOutput(string command) => new Executable(command).GetOutput("--version");
}

public enum GitDiscoveryStatus
{
    Found,
    NotFound,
    TooOld,
}

public sealed record GitDiscoveryResult(GitDiscoveryStatus Status, string? Command, GitVersion? Version);
