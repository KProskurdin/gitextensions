namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  What PuTTY's plink said went wrong in git's output, as upstream's <c>FormRemoteProcess</c> and <c>FormClone</c> read it.
/// </summary>
public enum PuttyFailureKind
{
    /// <summary>Nothing PuTTY can be asked about.</summary>
    None,

    /// <summary>The server refused every way to log in ("FATAL ERROR" about authentication): a key is missing.</summary>
    Authentication,

    /// <summary>plink asked whether to trust the host ("If you trust this host, enter "y"..."), which it cannot here.</summary>
    FingerprintNotRegistered,

    /// <summary>plink said that the server's host key is not cached in the registry.</summary>
    HostKeyNotCached,
}

/// <summary>
///  Upstream's Yes and No questions after a plink failure.
/// </summary>
public enum PuttyQuestion
{
    /// <summary>Upstream <c>MessageBoxes.CacheHostkey</c>.</summary>
    CacheHostKey,

    /// <summary>Upstream <c>FormRemoteProcess._fingerprintNotRegistredText</c>.</summary>
    RegisterFingerprint,
}

/// <summary>
///  The answer of upstream's <c>FormPuttyError</c> when it is not cancelled: Retry, with the key loaded into Pageant if the
///  user loaded one.
/// </summary>
public sealed record PuttyKeyAnswer(string? KeyPath);

/// <summary>
///  What to do after a plink failure: run the operation again, and the key the user loaded, if any.
/// </summary>
public sealed record PuttyRecovery(bool Retry, string? KeyPath)
{
    /// <summary>
    ///  The failure stands.
    /// </summary>
    public static PuttyRecovery None { get; } = new(false, null);
}

/// <summary>
///  The windows upstream shows after a plink failure, which the app supplies.
/// </summary>
public interface IPuttyPrompts
{
    /// <summary>
    ///  Upstream's <c>FormPuttyError.AskForKey</c>: "Please load your SSH private key", with Load SSH key, Retry and
    ///  Cancel; null for Cancel.
    /// </summary>
    Task<PuttyKeyAnswer?> AskForKeyAsync();

    /// <summary>
    ///  One of upstream's questions, with its texts (<see cref="PuttyFailure"/>); true for Yes.
    /// </summary>
    Task<bool> ConfirmAsync(PuttyQuestion question);

    /// <summary>
    ///  Upstream's <c>Plink.Connect</c>: plink connects to <paramref name="url"/> in a console window, where the user
    ///  answers its host key question; true when the window ends with exit code 0.
    /// </summary>
    Task<bool> ConnectAsync(string url);
}

/// <summary>
///  Upstream's handling of plink's failures: <c>FormRemoteProcess.HandleOnExit</c> and <c>DataReceived</c> for the
///  output window's operations, <c>FormClone.UpdateBranches</c> for the branches the clone window reads.
/// </summary>
public static class PuttyFailure
{
    /// <summary>
    ///  Upstream's caption of the host key question (<c>MessageBoxes.CacheHostkey</c>).
    /// </summary>
    public const string HostKeyNotCachedCaption = "SSH";

    /// <summary>
    ///  Upstream's host key question (<c>MessageBoxes._serverHostkeyNotCachedText</c>).
    /// </summary>
    public const string HostKeyNotCachedText =
        "The server's host key is not cached in the registry.\n\nDo you want to trust this host key and then try again?";

    /// <summary>
    ///  Upstream's caption of the fingerprint question (<c>FormRemoteProcess._fingerprintNotRegistredTextCaption</c>).
    /// </summary>
    public const string FingerprintNotRegisteredCaption = "Host Fingerprint not registered";

    // What plink writes, as upstream looks for it.
    private const string FatalError = "FATAL ERROR";
    private const string AuthenticationWord = "authentication";
    private const string HostKeyNotCachedOutput = "the server's host key is not cached in the registry";
    private const string TrustHostOutput = "If you trust this host, enter \"y\" to add the key to";

    /// <summary>
    ///  Upstream's fingerprint question (<c>FormRemoteProcess._fingerprintNotRegistredText</c>).
    /// </summary>
    public static string FingerprintNotRegisteredText { get; } = string.Join(Environment.NewLine,
        "The fingerprint of this host is not registered by PuTTY.",
        "This causes this process to hang, and that why it is automatically stopped.",
        "",
        "When the connection is opened detached from Git and Git Extensions, the host's fingerprint can be registered.",
        "You could also manually add the host's fingerprint or run Test Connection from the remotes dialog.",
        "",
        "Do you want to register the host's fingerprint and restart the process?");

    /// <summary>
    ///  What <paramref name="output"/> says went wrong, in upstream's order: a failed authentication, plink's question
    ///  whether to trust the host, then a host key not cached.
    /// </summary>
    public static PuttyFailureKind Classify(string output)
    {
        if (output.Contains(FatalError, StringComparison.Ordinal) &&
            output.Contains(AuthenticationWord, StringComparison.Ordinal))
        {
            return PuttyFailureKind.Authentication;
        }

        if (output.Contains(TrustHostOutput, StringComparison.Ordinal))
        {
            return PuttyFailureKind.FingerprintNotRegistered;
        }

        return output.Contains(HostKeyNotCachedOutput, StringComparison.OrdinalIgnoreCase)
            ? PuttyFailureKind.HostKeyNotCached
            : PuttyFailureKind.None;
    }

    /// <summary>
    ///  Upstream's questions for <paramref name="kind"/>, and what it does on Yes:
    ///  <list type="bullet">
    ///   <item>a failed authentication: <c>FormPuttyError</c>; Retry runs again, with the key the user loaded;</item>
    ///   <item>plink's trust question: the fingerprint question; Yes connects with plink to the URL so that the user can
    ///   register the host, then runs again whatever plink returned;</item>
    ///   <item>a host key not cached: with a URL, the host key question; Yes connects with plink, and runs again when
    ///   plink's window ended well (<c>AskForCacheHostkey</c>).</item>
    ///  </list>
    ///  <paramref name="url"/> is read only when a question needs it: the URL being connected to, else the remote's URL,
    ///  else the remote's name, as upstream.
    /// </summary>
    public static async Task<PuttyRecovery> RecoverAsync(IPuttyPrompts prompts, PuttyFailureKind kind,
        Func<string?> url)
    {
        switch (kind)
        {
            case PuttyFailureKind.Authentication:
                return await prompts.AskForKeyAsync() is { } answer
                    ? new PuttyRecovery(true, answer.KeyPath)
                    : PuttyRecovery.None;
            case PuttyFailureKind.FingerprintNotRegistered:
                if (!await prompts.ConfirmAsync(PuttyQuestion.RegisterFingerprint))
                {
                    return PuttyRecovery.None;
                }

                if (url() is { Length: > 0 } host)
                {
                    await prompts.ConnectAsync(host);
                }

                return new PuttyRecovery(true, null);
            case PuttyFailureKind.HostKeyNotCached:
                return url() is { Length: > 0 } remote && await prompts.ConfirmAsync(PuttyQuestion.CacheHostKey)
                    ? new PuttyRecovery(await prompts.ConnectAsync(remote), null)
                    : PuttyRecovery.None;
            default:
                return PuttyRecovery.None;
        }
    }
}
