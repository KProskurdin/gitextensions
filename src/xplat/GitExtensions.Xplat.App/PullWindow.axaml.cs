using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;
using MessageBoxDefaultButton = System.Windows.Forms.MessageBoxDefaultButton;
using DialogResult = System.Windows.Forms.DialogResult;
using TaskDialogButton = System.Windows.Forms.TaskDialogButton;
using TaskDialogPage = System.Windows.Forms.TaskDialogPage;

namespace GitExtensions.Xplat.App;

/// <summary>
///  The repository's git questions the pull window asks before it closes: a branch's <c>branch.&lt;name&gt;.remote</c>, and
///  whether a rebase would rebase a merge commit (remote, remote branch, current branch). <see cref="ManageRemotes"/>
///  opens the remotes window over the pull window with a remote chosen (or none) and lists the remotes again.
/// </summary>
public sealed record PullWindowGit(
    Func<string, Task<string?>> BranchRemoteOf,
    Func<string, string, string, Task<bool>> RebasesMergeCommit,
    Func<Window, string?, Task<IReadOnlyList<string>>>? ManageRemotes = null);

/// <summary>
///  Upstream's <c>FormPull</c> in the new shell: where to pull from, the branches, merge, rebase or fetch, the tags and
///  prune options. Closes with the request to run, or null when closed without pulling. The checks before pulling are
///  upstream's: the choices are complete, a rebase of a merge commit is confirmed, a pull with a detached HEAD is
///  confirmed, and a missing remote branch is offered as the local branch's name.
/// </summary>
public partial class PullWindow : Window
{
    private readonly PullDialog _dialog;
    private readonly IAppPreferences _preferences;
    private readonly PullWindowGit _git;
    private readonly IReadOnlyList<string> _remoteBranches;
    private readonly IReadOnlyList<string> _localBranches;
    private readonly string _displayPath;
    private bool _showing;

    /// <param name="remoteBranches">The remote-tracking branches ("origin/main") the remote branch box offers.</param>
    /// <param name="localBranches">The local branches it offers when pulling from a URL.</param>
    /// <param name="isShallow">True when the repository is a shallow clone, which offers the full history.</param>
    /// <param name="solveConflicts">Opens the conflicts window (upstream's "Solve conflicts").</param>
    /// <param name="manageStashes">Opens the stash window over this one (upstream's "Stash changes").</param>
    public PullWindow(PullDialog dialog, IAppPreferences preferences, string displayPath,
        IReadOnlyList<string> remoteBranches, IReadOnlyList<string> localBranches, bool isShallow, PullWindowGit git,
        Action? solveConflicts = null, Func<Window, Task>? manageStashes = null)
    {
        _dialog = dialog;
        _preferences = preferences;
        _git = git;
        _remoteBranches = remoteBranches;
        _localBranches = localBranches;
        _displayPath = displayPath;
        InitializeComponent();
        InitialFocus.Attach(this, () => RemotesBox);
        HelpImage.Load(preferences, "Pull");
        HelpImage.NoticeText = UpstreamTranslation.ByText(PullDialog.HoverShowImageText);
        dialog.AutoStash = preferences.AutoStash;
        RemotesBox.ItemsSource = dialog.Remotes;
        UnshallowCheck.IsVisible = isShallow;

        StashButton.IsEnabled = manageStashes is not null;
        StashButton.Click += (_, _) => UiActions.Run(() => manageStashes?.Invoke(this) ?? Task.CompletedTask, _ => { });
        AddRemoteButton.IsEnabled = git.ManageRemotes is not null;
        AddRemoteButton.Click += (_, _) => UiActions.Run(ManageRemotesAsync, _ => { });
        MergetoolButton.IsEnabled = solveConflicts is not null;
        MergetoolButton.Click += (_, _) => solveConflicts?.Invoke();

        ShowDialogState();
        RemotesBox.SelectionChanged += (_, _) => Edit(() => _dialog.Remote = RemotesBox.SelectedItem as string ?? "");
        PullFromRemoteRadio.IsCheckedChanged +=
            (_, _) => Edit(() => _dialog.FromUrl = PullFromUrlRadio.IsChecked == true);
        PullFromUrlRadio.IsCheckedChanged += (_, _) => Edit(() => _dialog.FromUrl = PullFromUrlRadio.IsChecked == true);
        MergeRadio.IsCheckedChanged += (_, _) => ChooseAction(MergeRadio, PullAction.Merge);
        RebaseRadio.IsCheckedChanged += (_, _) => ChooseAction(RebaseRadio, PullAction.Rebase);
        FetchRadio.IsCheckedChanged += (_, _) => ChooseAction(FetchRadio, PullAction.FetchOnly);
        RemoteBranchBox.DropDownOpened += (_, _) => ShowRemoteBranches();
        BrowseUrlButton.Click += (_, _) => UiActions.Run(BrowseUrlAsync, _ => { });
        PullButton.Click += (_, _) => UiActions.Run(PullAsync, ex => Title = ex.Message);
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close(null);
            }
        };
    }

    /// <summary>
    ///  The dialog state, for tests.
    /// </summary>
    public PullDialog Dialog => _dialog;

    private void ChooseAction(RadioButton radio, PullAction action)
    {
        if (radio.IsChecked == true)
        {
            Edit(() => _dialog.SetAction(action));
        }
    }

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
        _dialog.Url = UrlBox.Text?.Trim() ?? "";
        _dialog.RemoteBranch = RemoteBranchBox.Text?.Trim() ?? "";
        if (_dialog.IsFetch)
        {
            _dialog.LocalBranch = LocalBranchBox.Text?.Trim() ?? "";
        }

        _dialog.Tags = AllTagsRadio.IsChecked == true ? PullTags.All
            : NoTagsRadio.IsChecked == true ? PullTags.None
            : PullTags.Reachable;
        _dialog.Unshallow = UnshallowCheck.IsChecked == true;
        _dialog.Prune = PruneCheck.IsChecked == true;
        _dialog.PruneTags = PruneTagsCheck.IsChecked == true;
        _dialog.AutoStash = AutoStashCheck.IsChecked == true;
    }

    // Upstream's AddRemoteClick: the remotes window with the chosen remote (none for "[ All ]"), then the remotes again with
    // the same choice.
    private async Task ManageRemotesAsync()
    {
        if (_git.ManageRemotes is null)
        {
            return;
        }

        string chosen = _dialog.Remote;
        IReadOnlyList<string> remotes = await _git.ManageRemotes(this, _dialog.IsPullAll ? null : chosen);
        _dialog.UpdateRemotes(remotes, chosen, branchRemote: null);
        RemotesBox.ItemsSource = _dialog.Remotes;
        ShowDialogState();
    }

    private void ShowDialogState()
    {
        _showing = true;
        PullFromRemoteRadio.IsChecked = !_dialog.FromUrl;
        PullFromUrlRadio.IsChecked = _dialog.FromUrl;
        RemotesBox.SelectedItem = _dialog.Remote;
        RemotesBox.IsEnabled = !_dialog.FromUrl;
        UrlBox.IsEnabled = BrowseUrlButton.IsEnabled = _dialog.FromUrl;
        UrlBox.Text = _dialog.Url;
        RemoteBranchBox.Text = _dialog.RemoteBranch;
        LocalBranchBox.Text = _dialog.LocalBranch;
        LocalBranchBox.IsEnabled = _dialog.FetchOptionsEnabled;
        MergeRadio.IsChecked = _dialog.Action == PullAction.Merge;
        RebaseRadio.IsChecked = _dialog.Action == PullAction.Rebase;
        FetchRadio.IsChecked = _dialog.IsFetch;
        MergeRadio.IsEnabled = RebaseRadio.IsEnabled = _dialog.CanMergeOrRebase;
        ReachableTagsRadio.IsChecked = _dialog.Tags == PullTags.Reachable;
        NoTagsRadio.IsChecked = _dialog.Tags == PullTags.None;
        AllTagsRadio.IsChecked = _dialog.Tags == PullTags.All;
        AllTagsRadio.IsEnabled = PruneCheck.IsEnabled = PruneTagsCheck.IsEnabled = _dialog.FetchOptionsEnabled;
        UnshallowCheck.IsChecked = _dialog.Unshallow;
        PruneCheck.IsChecked = _dialog.Prune;
        PruneTagsCheck.IsChecked = _dialog.PruneTags;
        AutoStashCheck.IsChecked = _dialog.AutoStash;
        PullButtonText.Text = UpstreamTranslation.ByText(_dialog.ButtonText).Replace('&', '_');
        Title = string.Format(
            UpstreamTranslation.ByText(_dialog.IsFetch ? PullDialog.FormTitleFetch : PullDialog.FormTitlePull),
            _displayPath);
        (string image, string? hoverImage) = _dialog.HelpImages;
        HelpImage.Image1 = UpstreamImages.Named(image);
        HelpImage.Image2 = hoverImage is null ? null : UpstreamImages.Named(hoverImage);
        HelpImage.IsOnHoverShowImage2 = hoverImage is not null;
        _showing = false;
    }

    // Upstream's BranchesDropDown: the branches the repository knows of the remote (local ones for a URL), and an empty
    // entry first.
    private void ShowRemoteBranches()
    {
        string typed = RemoteBranchBox.Text ?? "";
        string prefix = _dialog.Remote + "/";
        IEnumerable<string> branches = _dialog.FromUrl
            ? _localBranches
            : _remoteBranches.Where(branch => branch.StartsWith(prefix, StringComparison.CurrentCultureIgnoreCase))
                .Select(branch => branch[prefix.Length..]).Reverse();
        RemoteBranchBox.ItemsSource = new[] { "" }.Concat(branches).ToList();
        RemoteBranchBox.Text = typed;
    }

    private async Task BrowseUrlAsync()
    {
        IReadOnlyList<IStorageFolder> folders =
            await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { AllowMultiple = false });
        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path)
        {
            UrlBox.Text = path;
        }
    }

    // Upstream's PullChanges, up to running the pull.
    private async Task PullAsync()
    {
        KeepTypedValues();
        if (_dialog.ValidationError() is { } error)
        {
            await new MessageBoxWindow(UpstreamTranslation.ByText(error), UpstreamTranslation.ByText("Error"),
                MessageBoxButtons.OK).ShowDialog(this);
            return;
        }

        _preferences.FormPullAction = _dialog.PulledAction;
        _preferences.AutoStash = AutoStashCheck.IsChecked == true;

        // Asked only when a commit not on the remote yet is a merge.
        if (_dialog.Action == PullAction.Rebase && !_dialog.FromUrl &&
            await _git.RebasesMergeCommit(_dialog.Remote, _dialog.RemoteBranch, _dialog.CurrentBranch))
        {
            MessageBoxWindow question = new(UpstreamTranslation.ByText(PullDialog.RebaseMergeQuestion),
                UpstreamTranslation.ByText(PullDialog.RebaseMergeQuestionCaption), MessageBoxButtons.YesNoCancel,
                MessageBoxDefaultButton.Button2);
            await question.ShowDialog(this);
            if (question.Result == DialogResult.Cancel)
            {
                Close(null);
                return;
            }

            if (question.Result != DialogResult.Yes)
            {
                return;
            }
        }

        // Upstream also offers "Checkout branch" here; the new shell has no window to choose a branch in yet.
        TaskDialogButton continueButton = new(UpstreamTranslation.ByText(PullDialog.ContinueText));
        if (!_dialog.IsFetch && _dialog.RemoteBranch.Length == 0 && _dialog.IsDetached &&
            !await AskAsync(
                new TaskDialogPage
                {
                    Text = UpstreamTranslation.ByText(PullDialog.NotOnBranch),
                    Caption = UpstreamTranslation.ByText(PullDialog.NotOnBranchCaption),
                    Buttons = { TaskDialogButton.Cancel, continueButton },
                }, continueButton))
        {
            return;
        }

        Dictionary<string, string?> remotes = [];
        foreach (string branch in new[] { _dialog.LocalBranch, _dialog.CurrentBranch }.Distinct())
        {
            remotes[branch] = await _git.BranchRemoteOf(branch);
        }

        PullBranches branches = _dialog.CalculateBranches(branch => remotes.GetValueOrDefault(branch));
        if (branches.Question is { } text)
        {
            TaskDialogButton useLocalBranch = new(branches.QuestionButton!);
            if (!await AskAsync(new TaskDialogPage
                {
                    Text = text,
                    Caption = UpstreamTranslation.ByText(PullDialog.NoRemoteBranchCaption),
                    Heading = UpstreamTranslation.ByText(PullDialog.NoRemoteBranch),
                    Buttons = { TaskDialogButton.Cancel, useLocalBranch },
                }, useLocalBranch))
            {
                return;
            }

            branches = branches.Confirmed();
        }

        Close(_dialog.Request(branches));
    }

    private async Task<bool> AskAsync(TaskDialogPage page, TaskDialogButton yes)
    {
        MessageBoxWindow question = new(page);
        await question.ShowDialog(this);
        return question.ChosenButton == yes;
    }
}