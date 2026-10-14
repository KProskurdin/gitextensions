using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility.Git;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  Upstream's tag options of the pull dialog: follow the remote's <c>tagopt</c>, fetch no tag or fetch every tag.
/// </summary>
public enum PullTags
{
    Reachable,
    None,
    All,
}

/// <summary>
///  The branches a pull or fetch names, as upstream's <c>CalculateLocalBranch</c> works them out. When
///  <see cref="Question"/> is set, upstream asks first whether to use the local branch's name on the remote ("Pull from
///  origin/topic"); <see cref="Confirmed"/> gives the branches for a yes.
/// </summary>
public sealed record PullBranches(
    string? LocalBranch,
    string? RemoteBranch,
    string? Question = null,
    string? QuestionButton = null)
{
    /// <summary>
    ///  The branches when the user agrees to the question: the remote branch of the local branch's name.
    /// </summary>
    public PullBranches Confirmed() => this with { RemoteBranch = LocalBranch, Question = null, QuestionButton = null };
}

/// <summary>
///  Upstream's <c>FormPull</c> without its window: the remotes with "[ All ]", the choices it starts with, which options
///  apply to the chosen action, the checks before pulling, and the request it runs.
/// </summary>
public sealed class PullDialog
{
    public const string AllRemotes = "[ All ]";

    // Upstream's texts.
    public const string FormTitlePull = "Pull ({0})";
    public const string FormTitleFetch = "Fetch ({0})";
    public const string ButtonPull = "&Pull";
    public const string ButtonFetch = "&Fetch";
    public const string HoverShowImageText = "Hover to see scenario when fast forward is possible.";
    public const string SelectRemoteRepository = "Please select a remote repository";
    public const string SelectSourceDirectory = "Please select a source directory";
    public const string NoRemoteBranch = "You didn't specify a remote branch";
    public const string NoRemoteBranchCaption = "Remote branch not specified";
    public const string NoRemoteBranchButton = "Pull from {0}";
    public const string NoRemoteBranchForFetchButton = "Fetch from {0}";
    public const string RebaseMergeQuestionCaption = "Rebase merge commit?";
    public const string NotOnBranchCaption = "Not on a branch";
    public const string ContinueText = "Continue";

    public static readonly string FetchAllBranchesCanOnlyWithFetch =
        "You can only fetch all remote branches (*) without merge or rebase." + Environment.NewLine +
        "If you want to fetch all remote branches, choose fetch." + Environment.NewLine +
        "If you want to fetch and merge a branch, choose a specific branch.";

    public static readonly string NoRemoteBranchMainInstruction =
        "You asked to pull from the remote '{0}'," + Environment.NewLine +
        "but did not specify a remote branch." + Environment.NewLine +
        "Because this is not the default configured remote for your local branch," + Environment.NewLine +
        "you must specify a remote branch.";

    public static readonly string NoRemoteBranchForFetchMainInstruction =
        "You asked to fetch from the remote '{0}'," + Environment.NewLine +
        "but did not specify a remote branch." + Environment.NewLine +
        "Because this is not the current branch, you must specify a remote branch.";

    public static readonly string RebaseMergeQuestion =
        "The current commit is a merge." + Environment.NewLine + "Are you sure you want to rebase this merge?";

    public static readonly string NotOnBranch =
        "You cannot \"pull\" when git head detached." + Environment.NewLine + Environment.NewLine +
        "Do you want to continue?";

    private PullAction _action;

    /// <param name="remotes">The repository's remotes.</param>
    /// <param name="currentBranch">The checked out branch, or upstream's detached head text.</param>
    /// <param name="action">The action asked for; <see cref="GitPullAction.None"/> takes <paramref name="defaultAction"/>.</param>
    /// <param name="defaultAction">Upstream's <c>DefaultPullAction</c>.</param>
    /// <param name="defaultRemote">The remote asked for; null takes <paramref name="branchRemote"/>.</param>
    /// <param name="branchRemote">The current branch's <c>branch.&lt;name&gt;.remote</c>.</param>
    public PullDialog(IReadOnlyList<string> remotes, string currentBranch, GitPullAction action,
        GitPullAction defaultAction, string? defaultRemote, string? branchRemote)
    {
        Remotes = [AllRemotes, .. remotes];
        CurrentBranch = currentBranch;

        // Upstream's BindRemotesDropDown: the remote asked for or the branch's, else the first one, else all.
        string? wanted = string.IsNullOrEmpty(defaultRemote) ? branchRemote : defaultRemote;
        Remote = remotes.FirstOrDefault(remote => StringComparer.OrdinalIgnoreCase.Equals(remote, wanted))
                 ?? (remotes.Count > 0 ? remotes[0] : AllRemotes);

        if (action == GitPullAction.None)
        {
            action = defaultAction;
        }

        switch (action)
        {
            case GitPullAction.Rebase:
                SetAction(PullAction.Rebase);
                break;
            case GitPullAction.Fetch:
                SetAction(PullAction.FetchOnly);
                break;
            case GitPullAction.FetchAll:
                SetAction(PullAction.FetchOnly);
                Remote = AllRemotes;
                break;
            case GitPullAction.FetchPruneAll:
                SetAction(PullAction.FetchOnly);
                Prune = true;
                if (string.IsNullOrEmpty(defaultRemote))
                {
                    Remote = AllRemotes;
                }

                break;
            default:
                SetAction(PullAction.Merge);
                break;
        }
    }

    /// <summary>
    ///  "[ All ]" and the repository's remotes, as upstream's list shows them.
    /// </summary>
    public IReadOnlyList<string> Remotes { get; private set; }

    public string CurrentBranch { get; }

    /// <summary>
    ///  Lists the remotes again after the remotes window closed, as upstream's <c>BindRemotesDropDown</c>: the remote
    ///  chosen before (else <paramref name="branchRemote"/>) when it still exists, else the first remote, else "[ All ]".
    /// </summary>
    public void UpdateRemotes(IReadOnlyList<string> remotes, string? chosen, string? branchRemote)
    {
        Remotes = [AllRemotes, .. remotes];
        string? wanted = string.IsNullOrEmpty(chosen) ? branchRemote : chosen;
        Remote = remotes.FirstOrDefault(remote => StringComparer.OrdinalIgnoreCase.Equals(remote, wanted))
                 ?? (remotes.Count > 0 ? remotes[0] : AllRemotes);
    }

    /// <summary>
    ///  True when HEAD is detached: no current branch, or upstream's detached head text.
    /// </summary>
    public bool IsDetached => CurrentBranch.Length == 0 || DetachedHeadParser.IsDetachedHead(CurrentBranch);

    /// <summary>
    ///  True to pull from <see cref="Url"/> (upstream's "URL" choice) instead of <see cref="Remote"/>.
    /// </summary>
    public bool FromUrl { get; set; }

    public string Remote { get; set; }

    public string Url { get; set; } = "";

    public string RemoteBranch { get; set; } = "";

    /// <summary>
    ///  The local branch box: the current branch for a merge or rebase (not editable), empty and editable for a fetch.
    /// </summary>
    public string LocalBranch { get; set; } = "";

    public PullAction Action => _action;

    public PullTags Tags { get; set; }

    public bool Unshallow { get; set; }

    public bool Prune { get; set; }

    public bool PruneTags { get; set; }

    public bool AutoStash { get; set; }

    /// <summary>
    ///  True when every remote is fetched (upstream's "[ All ]").
    /// </summary>
    public bool IsPullAll => !FromUrl && Remote.Equals(AllRemotes, StringComparison.InvariantCultureIgnoreCase);

    /// <summary>
    ///  Merge and rebase need one remote; all remotes can only be fetched.
    /// </summary>
    public bool CanMergeOrRebase => FromUrl || !IsPullAll;

    public bool IsFetch => _action == PullAction.FetchOnly;

    /// <summary>
    ///  The local branch, "Fetch all tags" and the prune options apply to a fetch only.
    /// </summary>
    public bool FetchOptionsEnabled => IsFetch;

    public string ButtonText => IsFetch ? ButtonFetch : ButtonPull;

    public string Title(string displayPath) => string.Format(IsFetch ? FormTitleFetch : FormTitlePull, displayPath);

    /// <summary>
    ///  Upstream's help picture for the action, and the one shown on hover (a fast forward merge), if any.
    /// </summary>
    public (string Image, string? HoverImage) HelpImages => _action switch
    {
        PullAction.Merge => ("HelpPullMerge", "HelpPullMergeFastForward"),
        PullAction.Rebase => ("HelpPullRebase", null),
        _ => ("HelpPullFetch", null),
    };

    /// <summary>
    ///  Chooses the action as upstream's radio buttons do: a merge or rebase pulls into the current branch and fetches
    ///  no more than the reachable tags; a fetch starts with no local branch.
    /// </summary>
    public void SetAction(PullAction action)
    {
        _action = action;
        if (action == PullAction.FetchOnly)
        {
            LocalBranch = "";
            return;
        }

        LocalBranch = CurrentBranch;
        if (Tags == PullTags.All)
        {
            Tags = PullTags.Reachable;
        }
    }

    /// <summary>
    ///  The message upstream shows instead of pulling, or null when the choices can be pulled.
    /// </summary>
    public string? ValidationError()
    {
        if (FromUrl && string.IsNullOrEmpty(Url))
        {
            return SelectSourceDirectory;
        }

        if (!FromUrl && string.IsNullOrEmpty(Remote) && !IsPullAll)
        {
            return SelectRemoteRepository;
        }

        return !IsFetch && RemoteBranch == "*" ? FetchAllBranchesCanOnlyWithFetch : null;
    }

    /// <summary>
    ///  Upstream's <c>CalculateSource</c>: the URL, every remote, or the remote.
    /// </summary>
    public string Source => FromUrl ? Url : IsPullAll ? "--all" : Remote;

    /// <summary>
    ///  Upstream's <c>CalculateLocalBranch</c>. <paramref name="branchRemoteOf"/> gives a branch's
    ///  <c>branch.&lt;name&gt;.remote</c>. A question in the result is asked first; no answer cancels.
    /// </summary>
    public PullBranches CalculateBranches(Func<string, string?> branchRemoteOf)
    {
        if (IsPullAll)
        {
            return new PullBranches(null, null);
        }

        string remoteBranch = RemoteBranch;
        if (IsDetached)
        {
            return new PullBranches(null, remoteBranch);
        }

        string source = Source;
        Lazy<string> currentBranchRemote = new(() => branchRemoteOf(LocalBranch) ?? "");
        string? localBranch;
        if (CurrentBranch == LocalBranch)
        {
            localBranch = source == currentBranchRemote.Value || currentBranchRemote.Value.Length == 0
                ? remoteBranch.Length == 0 ? null : CurrentBranch
                : LocalBranch;
        }
        else
        {
            localBranch = LocalBranch;
        }

        if (remoteBranch.Length == 0 && !string.IsNullOrEmpty(localBranch) && source != currentBranchRemote.Value &&
            !IsFetch)
        {
            return new PullBranches(localBranch, remoteBranch, string.Format(NoRemoteBranchMainInstruction, source),
                string.Format(NoRemoteBranchButton, $"{source}/{localBranch}"));
        }

        if (remoteBranch.Length == 0 && !string.IsNullOrEmpty(localBranch) && IsFetch)
        {
            // A fetch of the current branch with no remote branch named fetches with no refspec.
            if (CurrentBranch == localBranch)
            {
                return new PullBranches(null, remoteBranch);
            }

            return new PullBranches(localBranch, remoteBranch,
                string.Format(NoRemoteBranchForFetchMainInstruction, source),
                string.Format(NoRemoteBranchForFetchButton, $"{source}/{localBranch}"));
        }

        return new PullBranches(localBranch, remoteBranch);
    }

    /// <summary>
    ///  The pull or fetch to run with the branches <see cref="CalculateBranches"/> gave, with upstream's arguments.
    /// </summary>
    public PullRequest Request(PullBranches branches)
        => new(Source, branches.RemoteBranch ?? "", _action,
            Prune: IsFetch && Prune,
            AutoStash: !IsFetch && AutoStash,
            LocalBranch: branches.LocalBranch ?? "",
            FetchTags: Tags switch
            {
                PullTags.All => true,
                PullTags.None => false,
                _ => null,
            },
            Unshallow: Unshallow,
            PruneTags: IsFetch && PruneTags);

    /// <summary>
    ///  True for a shallow clone, which upstream detects by git's <c>shallow</c> file; the dialog then offers the full
    ///  history.
    /// </summary>
    public static bool IsShallowRepository(string repositoryPath)
        => File.Exists(new GitModule(new GitExecutorProvider(new GitDirectoryResolver()), repositoryPath)
            .ResolveGitInternalPath("shallow"));

    /// <summary>
    ///  The action upstream stores as <c>FormPullAction</c> after a pull.
    /// </summary>
    public GitPullAction PulledAction => _action switch
    {
        PullAction.Merge => GitPullAction.Merge,
        PullAction.Rebase => GitPullAction.Rebase,
        _ => GitPullAction.Fetch,
    };
}
