namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  Lets the app answer the questions ssh and git ask while they talk to a remote (a key passphrase, a password, a user
///  name, a new host key): the new shell's version of upstream's native <c>GitExtSshAskPass</c>, which exists for Windows
///  only. Like upstream, only <c>SSH_ASKPASS</c> is set: git also asks through it for its own prompts, after
///  <c>GIT_ASKPASS</c> and <c>core.askPass</c>, so a user's own askpass and git's credential helpers still come first.
/// </summary>
public static class GitAskPass
{
    /// <summary>
    ///  Marks a process that git or ssh started as the askpass program. ssh runs <c>SSH_ASKPASS</c> with the prompt as its
    ///  only argument, so the app cannot be given a verb; it looks for this variable instead.
    /// </summary>
    public const string MarkerVariable = "GITEXTENSIONS_XPLAT_ASKPASS";

    private static readonly string[] _secretWords = ["password", "passphrase", "PIN"];

    /// <summary>
    ///  The variables for a git process whose prompts the app at <paramref name="executable"/> answers.
    ///  <c>SSH_ASKPASS_REQUIRE=force</c> makes ssh use it even without a display, as upstream sets it.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Environment(string executable)
        => new Dictionary<string, string>
        {
            ["SSH_ASKPASS"] = executable,
            ["SSH_ASKPASS_REQUIRE"] = "force",
            [MarkerVariable] = "1",
        };

    /// <summary>
    ///  True when the started process is the askpass program: the marker is set and the only argument is the prompt.
    /// </summary>
    public static bool IsAskPassRun(string? marker, IReadOnlyList<string> args)
        => marker == "1" && args.Count == 1;

    /// <summary>
    ///  True when the answer to <paramref name="prompt"/> is a secret and is typed hidden: a password, a passphrase or a
    ///  PIN. A user name or a yes/no question about a host key is typed in the clear.
    /// </summary>
    public static bool IsSecret(string prompt)
        => _secretWords.Any(word => prompt.Contains(word, word == "PIN" ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase));
}
