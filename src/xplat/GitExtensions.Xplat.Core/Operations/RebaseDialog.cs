using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  Upstream's <c>FormRebase</c> without its window: the branch to rebase on, the options and the specific range, which
///  options can be changed, the buttons a rebase in progress shows, the check before rebasing and the rebase command.
/// </summary>
public sealed class RebaseDialog
{
    // Upstream's texts.
    public const string ContinueRebaseText = "&Continue rebase";
    public const string SolveConflictsText = "&Solve conflicts";
    public const string SolveConflictsHighlightedText = ">&Solve conflicts<";
    public const string ContinueRebaseHighlightedText = ">&Continue rebase<";
    public const string NoBranchSelected = "Please select a branch";
    public const string NoBranchSelectedCaption = "Error";
    public const string HoverShowImageText = "Hover to see scenario when fast forward is possible.";

    private const string RerereResolution = "using previous resolution";
    private const string Aborted = "Aborted";

    /// <summary>
    ///  Upstream's <c>cboBranches</c>: the branch (or commit) to rebase on, the <c>--onto</c> one with a specific range.
    /// </summary>
    public string Onto { get; set; } = "";

    /// <summary>
    ///  Upstream's "Specific range": the commits after <see cref="From"/> up to <see cref="To"/> are rebased.
    /// </summary>
    public bool SpecificRange { get; set; }

    public string From { get; set; } = "";

    public string To { get; set; } = "";

    public bool Interactive { get; set; }

    public bool PreserveMerges { get; set; }

    public bool AutoSquash { get; set; }

    public bool AutoStash { get; set; }

    public bool IgnoreDate { get; set; }

    public bool CommitterDateIsAuthorDate { get; set; }

    public bool UpdateRefs { get; set; }

    // Upstream's ToggleDateCheckboxMutualExclusions and chkInteractive_CheckedChanged: either date option rules out the
    // other and the interactive, merge and autosquash options, and autosquash needs an interactive rebase.
    public bool CanIgnoreDate => !CommitterDateIsAuthorDate;

    public bool CanCommitterDateIsAuthorDate => !IgnoreDate;

    public bool CanInteractive => !IgnoreDate && !CommitterDateIsAuthorDate;

    public bool CanPreserveMerges => CanInteractive;

    public bool CanAutoSquash => Interactive && CanInteractive;

    /// <summary>
    ///  Upstream's <c>OkClick</c> check: a branch to rebase on is given. Returns the error to show, or null.
    /// </summary>
    public string? Validate() => string.IsNullOrEmpty(Onto) ? NoBranchSelected : null;

    /// <summary>
    ///  Upstream's <c>Commands.Rebase</c> as <c>OkClick</c> builds it: <c>--onto</c> with a complete specific range,
    ///  otherwise a rebase on <see cref="Onto"/>; <c>--update-refs</c> or <c>--no-update-refs</c> only when the choice
    ///  differs from git's <c>rebase.updaterefs</c> and git supports it.
    /// </summary>
    /// <param name="supportUpdateRefs">Upstream's <c>GitVersion.SupportUpdateRefs</c>.</param>
    /// <param name="configuredUpdateRefs">Git's <c>rebase.updaterefs</c>.</param>
    public ArgumentString Arguments(bool supportUpdateRefs, bool configuredUpdateRefs)
    {
        Commands.RebaseOptions options = new()
        {
            Interactive = Interactive,
            PreserveMerges = PreserveMerges,
            AutoSquash = AutoSquash,
            AutoStash = AutoStash,
            IgnoreDate = IgnoreDate,
            CommitterDateIsAuthorDate = CommitterDateIsAuthorDate,
            UpdateRefs = supportUpdateRefs && configuredUpdateRefs != UpdateRefs ? UpdateRefs : null,
        };
        if (SpecificRange && !string.IsNullOrWhiteSpace(From) && !string.IsNullOrWhiteSpace(To))
        {
            options.OnTo = Onto;
            options.From = From;
            options.BranchName = To;
        }
        else
        {
            options.BranchName = Onto;
        }

        return Commands.Rebase(options);
    }

    /// <summary>
    ///  Upstream's <c>EnableButtons</c>: while a rebase is in progress the branch cannot be changed and the steps show
    ///  instead of Rebase; with unresolved conflicts "Solve conflicts" takes the place of "Continue rebase".
    /// </summary>
    /// <param name="inRebase">Upstream's <c>InTheMiddleOfRebase</c>.</param>
    /// <param name="conflictedMerge">Upstream's <c>InTheMiddleOfConflictedMerge</c>.</param>
    /// <param name="dirty">Upstream's <c>IsDirtyDir</c>, which "Auto stash" needs when no rebase is in progress.</param>
    public static RebaseButtons Buttons(bool inRebase, bool conflictedMerge, bool dirty)
        => inRebase
            ? new RebaseButtons(CanChangeBranch: false, Rebase: false, AutoStash: false, Steps: true,
                Continue: !conflictedMerge, SolveConflicts: conflictedMerge, UnresolvedConflicts: conflictedMerge,
                Default: conflictedMerge ? RebaseDefaultButton.SolveConflicts : RebaseDefaultButton.Continue)
            : new RebaseButtons(CanChangeBranch: true, Rebase: true, AutoStash: dirty, Steps: false, Continue: false,
                SolveConflicts: false, UnresolvedConflicts: conflictedMerge,
                Default: conflictedMerge ? RebaseDefaultButton.SolveConflicts : RebaseDefaultButton.Rebase);

    /// <summary>
    ///  Upstream's <c>CanContinueAction</c>: git reused a recorded resolution (rerere) and nothing is left to resolve, so
    ///  the rebase continues by itself.
    /// </summary>
    public static bool CanContinue(string output, IGitModule module)
        => output.Contains(RerereResolution)
           && !output.AsSpan().TrimEnd().EndsWith(Aborted)
           && (module.InTheMiddleOfMerge() || module.InTheMiddleOfPatch() || module.InTheMiddleOfRebase())
           && !module.InTheMiddleOfConflictedMerge();
}

/// <summary>
///  The buttons of upstream's rebase dialog. <see cref="Steps"/> covers "Add files", "Commit...", "Edit todo...", "Skip
///  currently applying commit" and "Abort".
/// </summary>
public sealed record RebaseButtons(
    bool CanChangeBranch,
    bool Rebase,
    bool AutoStash,
    bool Steps,
    bool Continue,
    bool SolveConflicts,
    bool UnresolvedConflicts,
    RebaseDefaultButton Default);

/// <summary>
///  Upstream's <c>AcceptButton</c> of the rebase dialog, which is highlighted ("&gt;Continue rebase&lt;") when it is not
///  Rebase.
/// </summary>
public enum RebaseDefaultButton
{
    Rebase,
    Continue,
    SolveConflicts,
}
