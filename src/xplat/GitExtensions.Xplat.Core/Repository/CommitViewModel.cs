using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitCommands.Git;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Diff;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Presentation;
using GitExtensions.Xplat.Core.Scripts;
using GitExtensions.Xplat.Core.Settings;
using GitUI.ScriptsEngine;

namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  What <see cref="CommitViewModel"/> asks of its window and the app besides <see cref="IDialogHost"/>: the questions and
///  windows upstream's FormCommit opens, the scripts and the push.
/// </summary>
public sealed record CommitViewModelServices
{
    /// <summary>
    ///  Upstream's <c>MessageBoxes.ConfirmSuppressible</c>: the confirmation, its question, the confirm button's text and the
    ///  caption; true at once when the question is turned off.
    /// </summary>
    public required Func<Confirmation, string, string, string, Task<bool>> ConfirmSuppressible { get; init; }

    /// <summary>
    ///  A question of the commit message checks (Yes, No): the question and the caption.
    /// </summary>
    public required Func<string, string, Task<bool>> AskValidation { get; init; }

    /// <summary>
    ///  Runs the "before commit" or "after commit" scripts; false when one stopped the commit.
    /// </summary>
    public required Func<ScriptEvent, Task<bool>> RunScripts { get; init; }

    /// <summary>
    ///  Pushes, offering a pull or a force push when the push is rejected; true once pushed.
    /// </summary>
    public required Func<PushRequest, Task<bool>> Push { get; init; }

    /// <summary>
    ///  Upstream's reset changes dialog: whether the files are existing ones and new ones; the choice.
    /// </summary>
    public required Func<bool, bool, Task<ResetChangesAction>> AskResetChanges { get; init; }

    /// <summary>
    ///  The create branch dialog at HEAD.
    /// </summary>
    public required Func<Task> CreateBranch { get; init; }

    /// <summary>
    ///  Shows an error with its caption (upstream's commit template errors).
    /// </summary>
    public required Action<string, string?> ShowError { get; init; }

    /// <summary>
    ///  Reads the module's dependent facts: whether the repository is a submodule and whether git can stash staged changes.
    /// </summary>
    public Func<Task<(bool InSubmodule, bool SupportsStashStaged)>>? ReadModule { get; init; }

    /// <summary>
    ///  Upstream's <c>DoCommit</c>: stages the submodule's new commit in its superproject.
    /// </summary>
    public Func<Task>? StageInSuperproject { get; init; }

    /// <summary>
    ///  Resolves a revision to a commit (for "Reset soft"); null when it names none.
    /// </summary>
    public Func<string, Task<string?>>? ResolveCommit { get; init; }

    /// <summary>
    ///  Upstream's <c>commitAuthorStatus</c> link: the Git config settings page, where user.name and user.email are set.
    /// </summary>
    public Func<Task>? ShowGitConfigSettings { get; init; }

    /// <summary>
    ///  Upstream's <c>remoteNameLabel</c> link: the remotes window on the pull behavior of the local branch given, or of
    ///  none.
    /// </summary>
    public Func<string?, Task>? ShowRemotes { get; init; }

    /// <summary>
    ///  Upstream's SolveMergeconflicts button: the conflicts window, without its offer to commit.
    /// </summary>
    public Func<Task>? ShowConflicts { get; init; }

    /// <summary>
    ///  Upstream's ignored files' editors: <c>.gitignore</c>, or the repository's own exclude file when true; true once
    ///  saved.
    /// </summary>
    public Func<bool, Task<bool>>? EditIgnoredFiles { get; init; }

    /// <summary>
    ///  The commit templates' settings window.
    /// </summary>
    public Func<Task>? EditTemplates { get; init; }

    /// <summary>
    ///  Opens a web page in the browser (the Conventional Commits documentation).
    /// </summary>
    public Func<Uri, Task>? OpenUrl { get; init; }

    /// <summary>
    ///  Reads the working directory's changes with the ignored, the assumed-unchanged and the skip-worktree files as asked
    ///  (in this order); null lists the repository's changes only.
    /// </summary>
    public Func<bool, bool, bool, Task<IReadOnlyList<FileChange>>>? ReadWorkTreeChanges { get; init; }

    /// <summary>
    ///  A git config value of the repository as git resolves it; null reads none.
    /// </summary>
    public Func<string, Task<string>>? ReadGitSetting { get; init; }

    /// <summary>
    ///  Reads the repository's spelling dictionary; null leaves spelling off.
    /// </summary>
    public Func<Task<string>>? LoadDictionary { get; init; }

    /// <summary>
    ///  Keeps the dictionary chosen in the message box's menu for the repository.
    /// </summary>
    public Func<string, Task>? SaveDictionary { get; init; }
}

/// <summary>
///  The view model of upstream's <c>FormCommit</c>: the message (the prepared one, the draft, the template, amend's), the
///  commit kind, the options, the buttons' states, committing and pushing, resetting and the status bar's texts, what the
///  file lists list and their commands (<c>CommitViewModel.FileLists.cs</c>), and the menus filled as they open
///  (<c>CommitViewModel.Menus.cs</c>). It works on the main window's repository state and operations, so a change made
///  here refreshes both windows. The lists' trees and filters, the diff, auto-completion and the spelling check are the
///  window's.
/// </summary>
public sealed partial class CommitViewModel : DialogViewModel
{
    // Upstream FormCommit and GitUI TranslatedStrings texts.
    public const string CommitMessageDisabledText = "Commit Message is requested during commit";
    public const string EnterMessageText = "Enter commit message";
    public const string CommitText = "_Commit";
    public const string CommitAndPushText = "Commit & _push";
    public const string CommitAndForcePushText = "Commit & force _push";
    public const string PushText = "_Push";
    public const string AmendCommitCaption = "Amend commit";
    public const string AmendResetSoftQuestion =
        "You are about to rewrite history by Soft Reset to the previous commit.\nOnly use Amend / Reset if the commit has not been published yet!\n\nDo you want to continue?";

    public const string RebaseState = "A rebase is in progress: commit continues it only after the conflicts are staged.";
    public const string MergeState = "A merge is in progress: committing completes the merge.";
    public const string WorkingText = "Working...";

    // Upstream's GPG choices, by their index in the box.
    public const int GpgDefault = 0;
    public const int GpgNoSign = 1;
    public const int GpgSpecificKey = 3;

    private const string DefaultRemote = "origin";
    private const string ResetSoftRevision = "HEAD~1";

    // Upstream refreshes on activation; here at most every 2 seconds, as the browse window does.
    private static readonly TimeSpan _activationRefreshInterval = TimeSpan.FromSeconds(2);

    private readonly string _repositoryPath;
    private readonly RepositoryViewModel _repository;
    private readonly RepositoryOperationsViewModel _actions;
    private readonly IRepositoryService _repositoryService;
    private readonly IAppPreferences _preferences;
    private readonly Func<Task> _refresh;
    private readonly ICommitMessageStore _messages;
    private readonly CommitViewModelServices _services;
    private string _messageBeforeAmend = "";

    // Upstream's _commitTemplate: git's commit.template the message started from; empty when it did not.
    private string _commitTemplate = "";

    // True while the remembered Amend state is put back; the draft is then the amend message already.
    private bool _restoringAmend;

    // True while Amend is turned off after a commit or a failed read, which puts back no message.
    private bool _clearingAmend;
    private DateTime _lastActivationRefresh;

    public CommitViewModel(IDialogHost host, string repositoryPath, RepositoryViewModel repository,
        RepositoryOperationsViewModel actions, IRepositoryService repositoryService, IAppPreferences preferences,
        Func<Task> refresh, ICommitMessageStore messages, CommitViewModelServices services)
        : base(host)
    {
        _repositoryPath = repositoryPath;
        _repository = repository;
        _actions = actions;
        _repositoryService = repositoryService;
        _preferences = preferences;
        _refresh = refresh;
        _messages = messages;
        _services = services;

        // Upstream's UseFormCommitMessage, read once per window as upstream does: off, git asks for the message in its
        // editor.
        UseFormCommitMessage = preferences.UseFormCommitMessage;
        MessagePlaceholder = host.Translate(UseFormCommitMessage ? EnterMessageText : CommitMessageDisabledText);
        ShowCommitAndPush = preferences.ShowCommitAndPush;
        ShowResetAll = preferences.ShowResetAllChanges;
        ShowResetUnstaged = preferences.ShowResetWorkTreeChanges;
        Title = CommitStatusBar.Title(repository.CurrentBranch, repositoryPath);
        Grouping = new FileListGroupingViewModel(preferences);
        SelectionFilter = new SelectionFilterViewModel(preferences.CommitDialogSelectionFilter, () => _unstagedListed,
            host.Translate);
        _repository.PropertyChanged += OnRepositoryChanged;
        _actions.PropertyChanged += OnActionsChanged;
        ShowRepositoryState();
        StatusText = _actions.StatusMessage;
    }

    /// <summary>
    ///  Raised on git's thread as files are staged or unstaged: the number done since the last report. The window adds it
    ///  to <see cref="StageProgressValue"/> on its own thread.
    /// </summary>
    public event EventHandler<int>? ProgressReported;

    /// <summary>
    ///  Upstream's <c>Message.Focus()</c> after "Modify the commit message" and a soft reset: the window focuses the message.
    /// </summary>
    public event EventHandler? MessageFocusRequested;

    public bool UseFormCommitMessage { get; }

    public string MessagePlaceholder { get; }

    public bool ShowCommitAndPush { get; }

    public bool ShowResetAll { get; }

    public bool ShowResetUnstaged { get; }

    [ObservableProperty]
    public partial string Title { get; private set; }

    /// <summary>
    ///  The commit message; the window's text box binds it.
    /// </summary>
    [ObservableProperty]
    public partial string Message { get; set; } = "";

    /// <summary>
    ///  Upstream's <c>CommitKind</c>: a fixup, squash or amend commit started from the grid, until it is made.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanChangeMessage), nameof(IsModifyMessageVisible))]
    public partial CommitKind Kind { get; private set; }

    /// <summary>
    ///  Upstream's CommitKind setter: a fixup or squash message is read only.
    /// </summary>
    public bool CanChangeMessage => UseFormCommitMessage && CommitKinds.MessageCanBeChanged(Kind);

    /// <summary>
    ///  The button that makes a fixup or squash commit a normal one.
    /// </summary>
    public bool IsModifyMessageVisible => UseFormCommitMessage && !CommitKinds.MessageCanBeChanged(Kind);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CommitAndPushContent))]
    public partial bool Amend { get; set; }

    [ObservableProperty]
    public partial bool ResetAuthor { get; set; }

    /// <summary>
    ///  Upstream's Amend_CheckedChanged: "Reset soft" works only while amending and when HEAD has a parent.
    /// </summary>
    [ObservableProperty]
    public partial bool CanResetSoft { get; private set; } = true;

    /// <summary>
    ///  The author the commit is made with; empty for git's.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAuthorSet))]
    public partial string Author { get; set; } = "";

    /// <summary>
    ///  Upstream's author label: set while an author is typed; clicking it clears the author.
    /// </summary>
    public bool IsAuthorSet => !string.IsNullOrEmpty(Author);

    [ObservableProperty]
    public partial bool SignOff { get; set; }

    [ObservableProperty]
    public partial bool NoVerify { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGpgKeyVisible), nameof(CommitIcon))]
    public partial int GpgSignIndex { get; set; }

    [ObservableProperty]
    public partial string GpgKey { get; set; } = "";

    public bool IsGpgKeyVisible => GpgSignIndex == GpgSpecificKey;

    /// <summary>
    ///  Upstream's gpgSignCommitChanged: the commit button shows a key while the commit is signed.
    /// </summary>
    public string CommitIcon => GpgSignIndex > GpgDefault ? "Key" : "RepoStateClean";

    /// <summary>
    ///  Upstream's UpdateButtonStates: the push button pushes with force after an amend (CommitAndPushForcedWhenAmend), and
    ///  only pushes when there is nothing to commit.
    /// </summary>
    public string CommitAndPushContent
        => Amend && _preferences.CommitAndPushForcedWhenAmend ? CommitAndForcePushText
            : _repository.Changes.Count > 0 || Amend ? CommitAndPushText
            : PushText;

    [ObservableProperty]
    public partial bool IsStageInSuperprojectVisible { get; private set; }

    [ObservableProperty]
    public partial bool IsStashStagedVisible { get; private set; }

    public bool StageInSuperproject
    {
        get => _preferences.StageInSuperprojectAfterCommit;
        set => SavePreference(() => _preferences.StageInSuperprojectAfterCommit = value);
    }

    public bool CloseAfterEachCommit
    {
        get => _preferences.CloseCommitDialogAfterCommit;
        set => SavePreference(() => _preferences.CloseCommitDialogAfterCommit = value);
    }

    public bool CloseAfterAllCommitted
    {
        get => _preferences.CloseCommitDialogAfterLastCommit;
        set => SavePreference(() => _preferences.CloseCommitDialogAfterLastCommit = value);
    }

    public bool RefreshOnFocus
    {
        get => _preferences.RefreshCommitDialogOnFocus;
        set => SavePreference(() => _preferences.RefreshCommitDialogOnFocus = value);
    }

    public bool SelectStagedOnEnterMessage
    {
        get => _preferences.CommitDialogSelectStagedOnEnterMessage;
        set => SavePreference(() => _preferences.CommitDialogSelectStagedOnEnterMessage = value);
    }

    /// <summary>
    ///  The unstaged files selected in the window's list.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<FileChange> SelectedUnstaged { get; set; } = [];

    /// <summary>
    ///  The staged files selected in the window's list.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<FileChange> SelectedStaged { get; set; } = [];

    [ObservableProperty]
    public partial bool IsIdle { get; private set; }

    [ObservableProperty]
    public partial bool CanStage { get; private set; }

    [ObservableProperty]
    public partial bool CanStageAll { get; private set; }

    [ObservableProperty]
    public partial bool CanUnstage { get; private set; }

    [ObservableProperty]
    public partial bool CanUnstageAll { get; private set; }

    [ObservableProperty]
    public partial bool CanCommitAndPush { get; private set; }

    [ObservableProperty]
    public partial bool HasConflicts { get; private set; }

    [ObservableProperty]
    public partial bool HasChanges { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStateVisible))]
    public partial string StateText { get; private set; } = "";

    public bool IsStateVisible => StateText.Length > 0;

    [ObservableProperty]
    public partial string? StatusText { get; private set; }

    [ObservableProperty]
    public partial string BranchText { get; private set; } = "";

    [ObservableProperty]
    public partial string PushToText { get; private set; } = "";

    [ObservableProperty]
    public partial string CommitterText { get; private set; } = "";

    [ObservableProperty]
    public partial string StagedCountText { get; private set; } = "";

    [ObservableProperty]
    public partial bool IsStageProgressVisible { get; private set; }

    [ObservableProperty]
    public partial double StageProgressMaximum { get; private set; }

    [ObservableProperty]
    public partial double StageProgressValue { get; set; }

    /// <summary>
    ///  Whether the window keeps the typed message as the draft when it closes (not for a fixup or squash message).
    /// </summary>
    public bool KeepsDraft => CommitKinds.KeepsDraft(Kind);

    private bool PushOnly => !Amend && _repository.Changes.Count == 0;

    /// <summary>
    ///  Like upstream, the window starts from the prepared message: git's merge message while a merge is stopped, otherwise
    ///  the draft left last time (in this app or the WinForms one), otherwise git's commit.template. A message typed before
    ///  the read finished is kept. The window calls it when it opens.
    /// </summary>
    public Task LoadAsync() => GuardAsync(async () =>
    {
        _ = ReadModuleAsync();
        Run(ReadShowUntrackedAsync);
        Run(LoadDictionaryAsync);
        string message = await _messages.LoadAsync(_repositoryPath);
        if (UseFormCommitMessage && string.IsNullOrEmpty(Message))
        {
            if (message.Length > 0)
            {
                Message = message.TrimEnd();
            }
            else
            {
                await LoadCommitTemplateAsync();
            }
        }

        // As upstream: Amend comes back checked, not while a merge is stopped, and the draft stays as it was left.
        if (Kind == CommitKind.Normal && _preferences.RememberAmendCommitState && !_repository.IsMerging &&
            await _messages.LoadAmendAsync(_repositoryPath))
        {
            _restoringAmend = true;
            Amend = true;
            _restoringAmend = false;
        }
    });

    /// <summary>
    ///  Upstream's <c>StartFixupCommitDialog</c>, <c>StartSquashCommitDialog</c> and <c>StartAmendCommitDialog</c>: the
    ///  window makes a <paramref name="kind"/> commit for the commit whose <paramref name="subject"/> and
    ///  <paramref name="fullMessage"/> are given, starting from upstream's message for it instead of the draft.
    /// </summary>
    public void StartSpecialCommit(CommitKind kind, string subject, string fullMessage)
    {
        Kind = kind;
        if (UseFormCommitMessage && CommitKinds.Message(kind, subject, fullMessage) is { Length: > 0 } message)
        {
            Message = message;
        }
    }

    /// <summary>
    ///  On close, as upstream's FormCommit: the draft and whether Amend was checked.
    /// </summary>
    public async Task SaveDraftAsync()
    {
        string message = Message;
        bool amend = Amend;
        await _messages.SaveAsync(_repositoryPath, message);
        await _messages.SaveAmendAsync(_repositoryPath, _preferences.RememberAmendCommitState && amend);
    }

    /// <summary>
    ///  Stops following the repository once the window is closed.
    /// </summary>
    public void Detach()
    {
        _repository.PropertyChanged -= OnRepositoryChanged;
        _actions.PropertyChanged -= OnActionsChanged;
    }

    /// <summary>
    ///  Reads the options again when their menu opens, since the settings may have changed them.
    /// </summary>
    public void RefreshOptions()
    {
        foreach (string name in new[]
                 {
                     nameof(CloseAfterEachCommit), nameof(CloseAfterAllCommitted), nameof(RefreshOnFocus),
                     nameof(SelectStagedOnEnterMessage), nameof(StageInSuperproject),
                 })
        {
            OnPropertyChanged(name);
        }
    }

    /// <summary>
    ///  Upstream's refreshDialogOnFormFocus (RefreshArtificialCommitOnApplicationActivated).
    /// </summary>
    public void RefreshOnActivation()
    {
        if (!_preferences.RefreshCommitDialogOnFocus || !IsIdle
            || DateTime.UtcNow - _lastActivationRefresh < _activationRefreshInterval)
        {
            return;
        }

        _lastActivationRefresh = DateTime.UtcNow;
        Run(_refresh);
    }

    /// <summary>
    ///  Upstream's StageAllAccordingToFilter and UnstageAllFiles: <paramref name="shown"/> are the files a filtered list
    ///  shows, or null for all.
    /// </summary>
    public Task StageAllAsync(bool staged, IEnumerable<FileChange>? shown)
        => StageChangesAsync(staged, shown ?? _repository.Changes.Where(change => change.Staged == staged));

    /// <summary>
    ///  Upstream's commitTemplatesToolStripMenuItem: a template replaces the message.
    /// </summary>
    public void ApplyTemplate(CommitTemplate template) => Message = CommitTemplates.Apply(template, _repository.CurrentBranch);

    /// <summary>
    ///  Upstream's CommitMessageToolStripMenuItemDropDownOpening: the previous messages, read each time the menu opens.
    /// </summary>
    public async Task<IReadOnlyList<string>> PreviousMessagesAsync()
    {
        int maxCount = _preferences.CommitDialogNumberOfPreviousMessages;
        return PreviousCommitMessages.Merge(
            await PreviousCommitMessages.ReadAsync(_repositoryPath, maxCount, _preferences.CommitDialogShowOnlyMyMessages),
            _preferences.LastCommitMessage, maxCount);
    }

    /// <summary>
    ///  Upstream's generateListOfChangesInSubmodulesChangesToolStripMenuItem_Click, for the staged changes; null when there
    ///  are none.
    /// </summary>
    public async Task<string?> SubmoduleChangesAsync()
    {
        List<string> staged = [.. _repository.Changes.Where(change => change.Staged).Select(change => change.Path)];
        string message = await PreviousCommitMessages.SubmoduleChangesAsync(_repositoryPath, staged);
        return message.Length > 0 ? message : null;
    }

    /// <summary>
    ///  Upstream's UpdateBranchNameDisplayAsync, which also sets the title.
    /// </summary>
    public Task ShowBranchAsync() => GuardAsync(async () =>
    {
        CommitBranchStatus status = await CommitStatusBar.BranchAsync(_repositoryPath);
        BranchText = status.BranchText;
        PushToText = status.PushTo ?? "";
        Title = CommitStatusBar.Title(status.Branch, _repositoryPath);
    });

    /// <summary>
    ///  Upstream reads the committer when the window opens and is activated, and when the author box is left.
    /// </summary>
    public Task ShowCommitterAsync()
        => GuardAsync(async () => CommitterText = await CommitStatusBar.CommitterAsync(_repositoryPath, Author));

    // Upstream's commitAuthorStatus_Click: the settings, then the committer read again.
    [RelayCommand]
    private async Task OpenCommitterSettingsAsync()
    {
        if (_services.ShowGitConfigSettings is { } show)
        {
            await GuardAsync(show);
            await ShowCommitterAsync();
        }
    }

    // Upstream's remoteNameLabel_Click: the remotes window on the branch's pull behavior, then the branch read again.
    [RelayCommand]
    private async Task OpenRemotesAsync()
    {
        if (_services.ShowRemotes is not { } show)
        {
            return;
        }

        await GuardAsync(async () =>
        {
            CommitBranchStatus status = await CommitStatusBar.BranchAsync(_repositoryPath);
            await show(status.Branch.Length > 0 ? status.Branch : null);
        });
        await ShowBranchAsync();
    }

    partial void OnSelectedUnstagedChanged(IReadOnlyList<FileChange> value) => ShowRepositoryState();

    partial void OnSelectedStagedChanged(IReadOnlyList<FileChange> value) => ShowRepositoryState();

    partial void OnAmendChanged(bool value)
    {
        Run(ToggleAmendAsync);
        Run(ShowResetSoftAsync);
    }

    [RelayCommand]
    private Task CommitAsync() => GuardAsync(async () =>
    {
        bool unstagedLeft = _repository.Changes.Any(change => !change.Staged);
        if (await CommitChangesAsync())
        {
            await StageInSuperprojectAsync();
            CloseAfterCommit(unstagedLeft);
        }
    });

    // Pushes the current branch only when the commit succeeded, to the remote the branch tracks. As upstream, with nothing
    // to commit the button is "Push" and only pushes.
    [RelayCommand]
    private Task CommitAndPushAsync() => GuardAsync(async () =>
    {
        string branch = _repository.CurrentBranch;
        bool unstagedLeft = _repository.Changes.Any(change => !change.Staged);
        bool pushOnly = PushOnly;

        // Read before the commit, which clears Amend. As upstream's FormCommit: an amended commit replaces the pushed one,
        // so with CommitAndPushForcedWhenAmend it is pushed with force-with-lease.
        bool forced = Amend && _preferences.CommitAndPushForcedWhenAmend;
        if (branch.Length == 0 || (!pushOnly && !await CommitChangesAsync()))
        {
            return;
        }

        string remote = _repository.TrackingRemote ?? DefaultRemote;
        bool pushed = await _services.Push(forced
            ? new PushRequest(remote, branch, branch, ForcePushOptions.ForceWithLease, Track: false)
            : new PushRequest(remote, branch, "", ForcePushOptions.DoNotForce, Track: true));
        if (pushed && pushOnly && _preferences.CloseCommitDialogAfterCommit)
        {
            Host.Close(null);
        }
        else if (pushed && !pushOnly)
        {
            await StageInSuperprojectAsync();
            CloseAfterCommit(unstagedLeft);
        }
    });

    [RelayCommand]
    private Task StageAsync() => StageChangesAsync(staged: false, SelectedUnstaged);

    [RelayCommand]
    private Task UnstageAsync() => StageChangesAsync(staged: true, SelectedStaged);

    // Upstream's ResetChanges (GitUICommands.StartResetChangesDialog): the question is about the unstaged files in both
    // cases, as upstream passes them for "Reset all changes" too.
    [RelayCommand]
    private Task ResetChangesAsync(bool onlyWorkTree) => GuardAsync(async () =>
    {
        List<FileChange> unstaged = [.. _repository.Changes.Where(change => !change.Staged)];
        ResetChangesAction action = await _services.AskResetChanges(unstaged.Any(change => !IsNew(change)),
            unstaged.Any(IsNew));
        if (action != ResetChangesAction.Cancel)
        {
            await _actions.ResetAllChangesAsync(_repositoryPath, clean: action == ResetChangesAction.ResetAndDelete,
                onlyWorkTree);
        }

        return;

        static bool IsNew(FileChange change) => change.Kind is ChangeKind.Untracked or ChangeKind.Added;
    });

    // Upstream's ResetSoftClick: after the amend question, a soft reset to the previous commit; Amend is then unchecked.
    [RelayCommand]
    private Task ResetSoftAsync() => GuardAsync(async () =>
    {
        if (!await _services.ConfirmSuppressible(Confirmation.Amend, Host.Translate(AmendResetSoftQuestion), "Yes",
                Host.Translate(AmendCommitCaption)))
        {
            return;
        }

        if (await ResolveCommitAsync(ResetSoftRevision) is { } previous
            && await _actions.ResetAsync(_repositoryPath, previous, ResetMode.Soft))
        {
            Amend = false;
            MessageFocusRequested?.Invoke(this, EventArgs.Empty);
        }
    });

    [RelayCommand]
    private Task StashStagedAsync() => GuardAsync(() => _actions.StashStagedAsync(_repositoryPath));

    // Upstream's createBranchToolStripButton_Click: the create branch dialog at HEAD.
    [RelayCommand]
    private Task CreateBranchAsync() => GuardAsync(_services.CreateBranch);

    // Upstream's modifyCommitMessageButton: the fixup or squash commit becomes a normal one, and the message is next.
    [RelayCommand]
    private void ModifyMessage()
    {
        Kind = CommitKind.Normal;
        MessageFocusRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void ClearAuthor() => Author = "";

    // Upstream's constructor: the check box only in a submodule, and no "Stash staged" for a git without stash --staged.
    private async Task ReadModuleAsync()
    {
        if (_services.ReadModule is { } read)
        {
            (IsStageInSuperprojectVisible, IsStashStagedVisible) = await read();
        }
    }

    // Upstream's AssignCommitMessageFromTemplate, with its error messages.
    private async Task LoadCommitTemplateAsync()
    {
        CommitTemplateResult result = await GitCommitTemplate.LoadAsync(_repositoryPath);
        if (result.Error is { } error)
        {
            _services.ShowError(error, result.ErrorCaption);
        }

        if (result.Template is { Length: > 0 } template && string.IsNullOrEmpty(Message))
        {
            Message = template;
            _commitTemplate = template;
        }
    }

    // Upstream's Amend_CheckedChanged: "Reset soft" works only while amending and when HEAD has a parent.
    private async Task ShowResetSoftAsync()
    {
        if (!Amend)
        {
            ResetAuthor = false;
            return;
        }

        CanResetSoft = await ResolveCommitAsync(ResetSoftRevision) is not null;
    }

    // Amend starts from the HEAD message; unchecking puts back what was typed before.
    private async Task ToggleAmendAsync()
    {
        if (_clearingAmend)
        {
            return;
        }

        if (_restoringAmend)
        {
            _messageBeforeAmend = Message;
            return;
        }

        if (!Amend)
        {
            Message = _messageBeforeAmend;
            return;
        }

        _messageBeforeAmend = Message;
        try
        {
            Message = await _repositoryService.GetHeadMessageAsync(_repositoryPath);
        }
        catch (Exception ex)
        {
            ClearAmend();
            Host.ShowError(ex);
        }
    }

    private void ClearAmend()
    {
        _clearingAmend = true;
        Amend = false;
        _clearingAmend = false;
    }

    // As upstream's FormCommit: the "before commit" scripts can stop the commit; the "after commit" ones run once it is
    // done.
    private async Task<bool> CommitChangesAsync()
    {
        // Upstream's FormCommit questions: amend rewrites history, and a commit with no branch checked out (and no rebase
        // under way) can be lost.
        if (Amend && !await _services.ConfirmSuppressible(Confirmation.Amend, Confirmations.AmendQuestion, "Amend",
                "Amend commit"))
        {
            return false;
        }

        // Upstream's IsCommitMessageValid: each failed check of the commit message settings asks whether to go on.
        foreach (string question in UseFormCommitMessage
                     ? CommitMessageValidation.Questions(Message, _preferences.CommitValidation)
                     : [])
        {
            if (!await _services.AskValidation(question, CommitMessageValidation.Caption))
            {
                return false;
            }
        }

        bool detached = _repository.CurrentBranch.Length == 0 && !_repository.IsRebasing;
        if (detached && !await _services.ConfirmSuppressible(Confirmation.CommitWithoutBranch,
                Confirmations.NotOnBranchQuestion, "Continue", "Not on a branch"))
        {
            return false;
        }

        if (!await _services.RunScripts(ScriptEvent.BeforeCommit))
        {
            return false;
        }

        CommitExtras extras = new(
            NoVerify: NoVerify,
            GpgSign: GpgSignIndex switch
            {
                GpgDefault => null,
                GpgNoSign => false,
                _ => true,
            },
            GpgKeyId: GpgSignIndex == GpgSpecificKey ? GpgKey : "",
            ResetAuthor: Amend && ResetAuthor);
        bool committed;
        if (UseFormCommitMessage)
        {
            // As upstream: the typed message is kept in the settings, so the "Commit message" menu of any repository
            // offers it.
            _preferences.LastCommitMessage = Message;

            // As upstream: the template left as it was is no message, and its '#' lines are comments.
            string message = Message == _commitTemplate
                ? ""
                : CommitMessageFormat.Format(Message, _preferences.EnsureCommitMessageSecondLineEmpty,
                    usingCommitTemplate: _commitTemplate.Length > 0);
            committed = await _actions.CommitAsync(_repositoryPath, message, Amend, SignOff, Author, extras);
        }
        else
        {
            committed = await _actions.CommitWithEditorAsync(_repositoryPath, Amend, SignOff, Author, extras);
        }

        if (committed)
        {
            _messageBeforeAmend = "";
            Message = "";
            ClearAmend();
            NoVerify = false;
            Kind = CommitKind.Normal;
            await _messages.ResetAsync(_repositoryPath);
            await _services.RunScripts(ScriptEvent.AfterCommit);
        }

        return committed;
    }

    // Upstream's DoCommit: the window closes after each commit (CloseCommitDialogAfterCommit), or after the commit that
    // leaves no unstaged changes (CloseCommitDialogAfterLastCommit).
    private void CloseAfterCommit(bool unstagedLeft)
    {
        if (_preferences.CloseCommitDialogAfterCommit
            || (!unstagedLeft && _preferences.CloseCommitDialogAfterLastCommit))
        {
            Host.Close(null);
        }
    }

    // Upstream's DoCommit: the submodule's new commit is staged in its superproject.
    private async Task StageInSuperprojectAsync()
    {
        if (_preferences.StageInSuperprojectAfterCommit && _services.StageInSuperproject is { } stage)
        {
            await stage();
        }
    }

    private Task StageChangesAsync(bool staged, IEnumerable<FileChange> changes)
    {
        List<string> paths =
        [
            .. changes
                .Where(change => change.Kind != ChangeKind.Conflict)
                .SelectMany(change => change.IndexPaths)
        ];
        return paths.Count > 0 ? GuardAsync(() => StageFilesAsync(staged, paths)) : Task.CompletedTask;
    }

    // Upstream's toolStripProgressBar1: shown while git runs, and full only at the end, since refreshing the lists follows.
    private async Task StageFilesAsync(bool staged, IReadOnlyList<string> paths)
    {
        StageProgressMaximum = paths.Count;
        StageProgressValue = 0;
        IsStageProgressVisible = true;
        try
        {
            await (staged
                ? _actions.UnstageAsync(_repositoryPath, paths, Processed)
                : _actions.StageAsync(_repositoryPath, paths, Processed));
        }
        finally
        {
            IsStageProgressVisible = false;
        }

        // Called on git's thread; the window posts it to its own.
        void Processed(int count) => ProgressReported?.Invoke(this, count);
    }

    private Task<string?> ResolveCommitAsync(string revision)
        => _services.ResolveCommit is { } resolve
            ? resolve(revision)
            : RepositoryScriptContext.ResolveCommitAsync(_repositoryPath, revision);

    private void SavePreference(Action change)
    {
        change();
        _preferences.Save();
    }

    private void OnRepositoryChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(RepositoryViewModel.Changes):
                ShowRepositoryState();
                OnPropertyChanged(nameof(CommitAndPushContent));
                ReadListedChanges();
                break;
            case nameof(RepositoryViewModel.IsLoading):
            case nameof(RepositoryViewModel.IsMerging):
            case nameof(RepositoryViewModel.IsRebasing):
                ShowRepositoryState();
                break;
            case nameof(RepositoryViewModel.CurrentBranch):
                ShowRepositoryState();
                Run(ShowBranchAsync);
                break;
        }
    }

    private void OnActionsChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(RepositoryOperationsViewModel.IsBusy):
                StatusText = _actions.IsBusy ? WorkingText : _actions.StatusMessage;
                ShowRepositoryState();
                break;
            case nameof(RepositoryOperationsViewModel.StatusMessage):
                StatusText = _actions.StatusMessage;
                break;
        }
    }

    // The window's UpdateState: the buttons as the repository, the operations and the selections allow.
    private void ShowRepositoryState()
    {
        bool idle = !_actions.IsBusy && !_repository.IsLoading;
        IReadOnlyList<FileChange> changes = _repository.Changes;
        IsIdle = idle;
        CanStage = idle && SelectedUnstaged.Any(change => change.Kind != ChangeKind.Conflict);
        CanStageAll = idle && changes.Any(change => !change.Staged && change.Kind != ChangeKind.Conflict);
        CanUnstage = idle && SelectedStaged.Count > 0;
        CanUnstageAll = idle && changes.Any(change => change.Staged);
        HasConflicts = changes.Any(change => change.Kind == ChangeKind.Conflict);
        HasChanges = changes.Count > 0;
        CanCommitAndPush = idle && _repository.CurrentBranch.Length > 0;
        StateText = _repository.IsRebasing ? Host.Translate(RebaseState)
            : _repository.IsMerging ? Host.Translate(MergeState)
            : "";
        StagedCountText = CommitStatusBar.Staged(changes.Count(change => change.Staged), changes.Count);
    }
}
