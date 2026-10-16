using GitExtensions.Extensibility.Git;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  Upstream's <c>FormResetCurrentBranch</c> without its window: the reset types it offers, the question before a hard
///  reset, when submodules are updated afterwards and the git documentation its help button opens.
/// </summary>
public sealed class ResetCurrentBranchDialog
{
    // Upstream's texts.
    public const string BranchInfo = "Reset branch '{0}' to revision:";
    public const string ResetHardWarning = "You are about to discard ALL local changes, are you sure?";
    public const string ResetCaption = "Reset branch";

    private const string HelpUrlFormat = "https://git-scm.com/docs/git-reset#Documentation/git-reset.txt-{0}";

    /// <param name="mode">Upstream's <c>ResetType</c> to start with: soft unless the caller chooses another.</param>
    public ResetCurrentBranchDialog(ResetMode mode = ResetMode.Soft)
    {
        Mode = mode;
    }

    /// <summary>
    ///  Upstream's reset types, in its order.
    /// </summary>
    public static IReadOnlyList<ResetMode> Modes { get; } =
        [ResetMode.Soft, ResetMode.Mixed, ResetMode.Keep, ResetMode.Merge, ResetMode.Hard];

    public ResetMode Mode { get; set; }

    /// <summary>
    ///  Upstream asks <see cref="ResetHardWarning"/> before a hard reset only.
    /// </summary>
    public bool AsksFirst => Mode == ResetMode.Hard;

    /// <summary>
    ///  Upstream's <c>Ok_Click</c>: submodules are updated after a reset that moves HEAD when a hard reset did it, or when
    ///  upstream's <c>UpdateSubmodulesOnCheckout</c> is on and the repository has submodules.
    /// </summary>
    /// <param name="updateSubmodulesOnCheckout">Upstream's <c>UpdateSubmodulesOnCheckout</c>.</param>
    public bool UpdatesSubmodules(bool movesHead, bool? updateSubmodulesOnCheckout, bool hasSubmodules)
        => movesHead && (Mode == ResetMode.Hard || (updateSubmodulesOnCheckout is true && hasSubmodules));

    /// <summary>
    ///  Upstream's help button: git's documentation of the chosen reset type.
    /// </summary>
    public string HelpUrl()
        => string.Format(HelpUrlFormat, Mode switch
        {
            ResetMode.Mixed => "--mixed",
            ResetMode.Keep => "--keep",
            ResetMode.Merge => "--merge",
            ResetMode.Hard => "--hard",
            _ => "--soft",
        });
}
