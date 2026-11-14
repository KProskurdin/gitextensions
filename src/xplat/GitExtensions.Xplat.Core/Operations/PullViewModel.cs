using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitExtensions.Xplat.Core.Presentation;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  The view model of upstream's <c>FormPull</c> (rules in <see cref="PullDialog"/>): where to pull from, the branches,
///  merge, rebase or fetch, the tags and prune options. Closes its window with the <see cref="PullRequest"/> to run, or
///  null when closed without pulling. The checks before pulling are upstream's: the choices are complete, a rebase of a
///  merge commit is confirmed, a pull with a detached HEAD is confirmed, and a missing remote branch is offered as the
///  local branch's name.
/// </summary>
public sealed partial class PullViewModel : DialogViewModel
{
    private readonly IAppPreferences _preferences;
    private readonly IRemoteUrlHistory _remoteUrls;
    private readonly string _displayPath;
    private readonly IReadOnlyList<string> _remoteBranches;
    private readonly IReadOnlyList<string> _localBranches;
    private readonly PullViewModelGit _git;
    private bool _showing;

    /// <param name="dialog">The dialog state.</param>
    /// <param name="preferences">Upstream's pull action and auto stash.</param>
    /// <param name="remoteUrls">The remote URL history the URL box lists and a pulled folder is added to.</param>
    /// <param name="displayPath">The repository, for the title.</param>
    /// <param name="remoteBranches">The remote-tracking branches ("origin/main") the remote branch box offers.</param>
    /// <param name="localBranches">The local branches it offers when pulling from a URL.</param>
    /// <param name="isShallow">True when the repository is a shallow clone, which offers the full history.</param>
    /// <param name="git">The repository's questions and the windows the dialog opens.</param>
    public PullViewModel(IDialogHost host, PullDialog dialog, IAppPreferences preferences, IRemoteUrlHistory remoteUrls,
        string displayPath, IReadOnlyList<string> remoteBranches, IReadOnlyList<string> localBranches, bool isShallow,
        PullViewModelGit git)
        : base(host)
    {
        Dialog = dialog;
        _preferences = preferences;
        _remoteUrls = remoteUrls;
        _displayPath = displayPath;
        _remoteBranches = remoteBranches;
        _localBranches = localBranches;
        _git = git;
        IsShallow = isShallow;
        dialog.AutoStash = preferences.AutoStash;
        Remotes = dialog.Remotes;
        ShowDialogState();
    }

    /// <summary>
    ///  The dialog state, for tests.
    /// </summary>
    public PullDialog Dialog { get; }

    public bool IsShallow { get; }

    public bool CanManageRemotes => _git.ManageRemotes is not null;

    public bool CanSolveConflicts => _git.SolveConflicts is not null;

    public bool CanManageStashes => _git.ManageStashes is not null;

    [ObservableProperty]

    public partial string Title { get; private set; } = "";

    [ObservableProperty]

    public partial IReadOnlyList<string> Remotes { get; private set; }

    [ObservableProperty]

    public partial string? Remote { get; set; }

    [ObservableProperty]

    public partial bool FromUrl { get; set; }

    /// <summary>
    ///  The remote URL history the URL box lists.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> UrlHistory { get; private set; } = [];

    [ObservableProperty]

    public partial string? Url { get; set; }

    [ObservableProperty]

    public partial IReadOnlyList<string> RemoteBranchItems { get; private set; } = [];

    [ObservableProperty]

    public partial string? RemoteBranch { get; set; }

    [ObservableProperty]

    public partial string? LocalBranch { get; set; }

    [ObservableProperty]

    public partial PullAction Action { get; set; }

    [ObservableProperty]

    public partial bool CanMergeOrRebase { get; private set; }

    /// <summary>
    ///  Upstream's fetch options (the local branch, all tags and prune) are enabled only for a fetch.
    /// </summary>
    [ObservableProperty]
    public partial bool FetchOptionsEnabled { get; private set; }

    [ObservableProperty]

    public partial PullTags Tags { get; set; }

    [ObservableProperty]

    public partial bool Unshallow { get; set; }

    [ObservableProperty]

    public partial bool Prune { get; set; }

    [ObservableProperty]

    public partial bool PruneTags { get; set; }

    [ObservableProperty]

    public partial bool AutoStash { get; set; }

    /// <summary>
    ///  Pull or Fetch, with its access key.
    /// </summary>
    [ObservableProperty]
    public partial string PullButtonText { get; private set; } = "";

    /// <summary>
    ///  The help picture's image name, and the one shown on hover (null for none).
    /// </summary>
    [ObservableProperty]
    public partial (string Image, string? HoverImage) HelpImages { get; private set; }

    /// <summary>
    ///  Upstream's <c>UpdateFormTitleAndButton</c> when "Url" is chosen: the remote URL history, the typed text kept. The
    ///  window calls it when it opens.
    /// </summary>
    public Task LoadUrlHistoryAsync() => GuardAsync(async () =>
    {
        IList<GitCommands.UserRepositoryHistory.Repository> history = await _remoteUrls.LoadAsync();
        string? typed = Url;
        UrlHistory = [.. history.Select(repository => repository.Path)];
        Url = typed;
        OnPropertyChanged(nameof(Url));
    });

    /// <summary>
    ///  Upstream's <c>BranchesDropDown</c>: the branches the repository knows of the remote (local ones for a URL), and an
    ///  empty entry first. The window calls it when the list opens.
    /// </summary>
    public void ShowRemoteBranches()
    {
        string typed = RemoteBranch ?? "";
        string prefix = Dialog.Remote + "/";
        IEnumerable<string> branches = Dialog.FromUrl
            ? _localBranches
            : _remoteBranches.Where(branch => branch.StartsWith(prefix, StringComparison.CurrentCultureIgnoreCase))
                .Select(branch => branch[prefix.Length..]).Reverse();
        RemoteBranchItems = ["", .. branches];
        RemoteBranch = typed;
        OnPropertyChanged(nameof(RemoteBranch));
    }

    partial void OnRemoteChanged(string? value) => Edit(() => Dialog.Remote = value ?? "");

    partial void OnFromUrlChanged(bool value) => Edit(() => Dialog.FromUrl = value);

    partial void OnActionChanged(PullAction value) => Edit(() => Dialog.SetAction(value));

    // A change made in the window, then the window shown again from the state.
    private void Edit(Action change)
    {
        if (_showing)
        {
            return;
        }

        KeepTypedValues();
        change();
        ShowDialogState();
    }

    // The boxes the user types in are read into the state before it changes.
    private void KeepTypedValues()
    {
        Dialog.Url = Url?.Trim() ?? "";
        Dialog.RemoteBranch = RemoteBranch?.Trim() ?? "";
        if (Dialog.IsFetch)
        {
            Dialog.LocalBranch = LocalBranch?.Trim() ?? "";
        }

        Dialog.Tags = Tags;
        Dialog.Unshallow = Unshallow;
        Dialog.Prune = Prune;
        Dialog.PruneTags = PruneTags;
        Dialog.AutoStash = AutoStash;
    }

    private void ShowDialogState()
    {
        _showing = true;
        FromUrl = Dialog.FromUrl;
        Remote = Dialog.Remote;
        Url = Dialog.Url;
        RemoteBranch = Dialog.RemoteBranch;
        LocalBranch = Dialog.LocalBranch;
        FetchOptionsEnabled = Dialog.FetchOptionsEnabled;
        Action = Dialog.Action;
        CanMergeOrRebase = Dialog.CanMergeOrRebase;
        Tags = Dialog.Tags;
        Unshallow = Dialog.Unshallow;
        Prune = Dialog.Prune;
        PruneTags = Dialog.PruneTags;
        AutoStash = Dialog.AutoStash;
        PullButtonText = Host.Translate(Dialog.ButtonText).Replace('&', '_');
        Title = string.Format(Host.Translate(Dialog.IsFetch ? PullDialog.FormTitleFetch : PullDialog.FormTitlePull),
            _displayPath);
        HelpImages = Dialog.HelpImages;
        _showing = false;
    }

    // Upstream's AddRemoteClick: the remotes window with the chosen remote (none for "[ All ]"), then the remotes again
    // with the same choice.
    [RelayCommand]
    private Task ManageRemotesAsync() => GuardAsync(async () =>
    {
        if (_git.ManageRemotes is null)
        {
            return;
        }

        KeepTypedValues();
        string chosen = Dialog.Remote;
        IReadOnlyList<string> remotes = await _git.ManageRemotes(Dialog.IsPullAll ? null : chosen);
        Dialog.UpdateRemotes(remotes, chosen, branchRemote: null);
        _showing = true;
        Remotes = Dialog.Remotes;
        _showing = false;
        ShowDialogState();
    });

    [RelayCommand]
    private void SolveConflicts() => _git.SolveConflicts?.Invoke();

    [RelayCommand]
    private Task ManageStashesAsync() => GuardAsync(() => _git.ManageStashes?.Invoke() ?? Task.CompletedTask);

    [RelayCommand]
    private Task BrowseUrlAsync() => GuardAsync(async () =>
    {
        if (_git.PickFolder is not null && await _git.PickFolder() is { } path)
        {
            Url = path;
        }
    });

    // Upstream's PullChanges, up to running the pull.
    [RelayCommand]
    private Task PullAsync() => GuardAsync(async () =>
    {
        KeepTypedValues();
        if (Dialog.ValidationError() is { } error)
        {
            await Host.ShowMessageAsync(Host.Translate(error), Host.Translate("Error"));
            return;
        }

        _preferences.FormPullAction = Dialog.PulledAction;
        _preferences.AutoStash = AutoStash;

        // Asked only when a commit not on the remote yet is a merge.
        if (Dialog.Action == PullAction.Rebase && !Dialog.FromUrl &&
            await _git.RebasesMergeCommit(Dialog.Remote, Dialog.RemoteBranch, Dialog.CurrentBranch))
        {
            bool? rebase = await Host.AskAsync(Host.Translate(PullDialog.RebaseMergeQuestion),
                Host.Translate(PullDialog.RebaseMergeQuestionCaption), defaultIsNo: true);
            if (rebase is null)
            {
                Host.Close(null);
                return;
            }

            if (rebase == false)
            {
                return;
            }
        }

        // Upstream's detached HEAD question: check out a branch first (the pull then goes on), go on, or cancel, which
        // closes the dialog as upstream's Cancel result does.
        if (!Dialog.IsFetch && Dialog.RemoteBranch.Length == 0 && Dialog.IsDetached && !await _git.PullWhileDetached())
        {
            Host.Close(null);
            return;
        }

        // Upstream adds a pulled folder to the remote URL history.
        if (Dialog.FromUrl && Directory.Exists(Dialog.Url))
        {
            await _remoteUrls.AddAsMostRecentAsync(Dialog.Url);
        }

        Dictionary<string, string?> remotes = [];
        foreach (string branch in new[] { Dialog.LocalBranch, Dialog.CurrentBranch }.Distinct())
        {
            remotes[branch] = await _git.BranchRemoteOf(branch);
        }

        PullBranches branches = Dialog.CalculateBranches(branch => remotes.GetValueOrDefault(branch));
        if (branches.Question is { } text)
        {
            if (!await _git.UseLocalBranchName(text, branches.QuestionButton!))
            {
                return;
            }

            branches = branches.Confirmed();
        }

        Host.Close(Dialog.Request(branches));
    });
}

/// <summary>
///  The repository's git questions and the windows <see cref="PullViewModel"/> opens.
/// </summary>
/// <param name="BranchRemoteOf">A branch's <c>branch.&lt;name&gt;.remote</c>.</param>
/// <param name="RebasesMergeCommit">Whether a rebase would rebase a merge commit (remote, remote branch, current
///  branch).</param>
/// <param name="PullWhileDetached">Upstream's detached HEAD question: true to go on pulling, after checking out a branch
///  or not.</param>
/// <param name="UseLocalBranchName">Upstream's question for a missing remote branch (text, button): true to pull the
///  local branch's name.</param>
/// <param name="ManageRemotes">Opens the remotes window with a remote chosen (or none) and lists the remotes again.</param>
/// <param name="SolveConflicts">Opens the conflicts window (upstream's "Solve conflicts").</param>
/// <param name="ManageStashes">Opens the stash window (upstream's "Stash changes").</param>
/// <param name="PickFolder">Picks a folder to pull from.</param>
public sealed record PullViewModelGit(
    Func<string, Task<string?>> BranchRemoteOf,
    Func<string, string, string, Task<bool>> RebasesMergeCommit,
    Func<Task<bool>> PullWhileDetached,
    Func<string, string, Task<bool>> UseLocalBranchName,
    Func<string?, Task<IReadOnlyList<string>>>? ManageRemotes = null,
    Action? SolveConflicts = null,
    Func<Task>? ManageStashes = null,
    Func<Task<string?>>? PickFolder = null);
