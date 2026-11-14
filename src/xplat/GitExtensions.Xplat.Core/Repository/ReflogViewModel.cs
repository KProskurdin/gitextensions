using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitCommands.Git;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Presentation;

namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  The view model of upstream's <c>FormReflog</c>: the reflog of HEAD or a branch (reading in <see cref="Reflog"/>), with
///  upstream's menu to copy an entry's hash, create a branch on it or reset the current branch to it.
/// </summary>
public sealed partial class ReflogViewModel : DialogViewModel
{
    // Upstream's texts.
    public const string CurrentBranchFormat = "current branch ({0})";

    public const string ContinueResetWithChanges =
        "You have changes in your working directory that could be lost.\n\nDo you want to continue?";

    public const string ContinueResetCaption = "Changes not committed...";

    private readonly string _repositoryPath;
    private readonly IGitModule _module;
    private readonly Func<ObjectId, Task> _createBranch;
    private readonly Func<ObjectId, ResetMode, Task> _resetCurrentBranch;
    private readonly Func<string, string, Task<IReadOnlyList<ReflogEntry>>> _loadReflog;
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private string? _currentBranch;
    private bool _loadingReferences;

    /// <param name="repositoryPath">The repository whose reflogs are read.</param>
    /// <param name="module">The repository, for its state and references.</param>
    /// <param name="createBranch">Opens the create branch window on a commit, as upstream's reflog opens it.</param>
    /// <param name="resetCurrentBranch">Opens the reset current branch window on a commit with a reset type.</param>
    /// <param name="loadReflog">Reads a reference's reflog in a repository; <see cref="Reflog.LoadAsync"/> (git) when not
    /// given.</param>
    public ReflogViewModel(IDialogHost host, string repositoryPath, IGitModule module, Func<ObjectId, Task> createBranch,
        Func<ObjectId, ResetMode, Task> resetCurrentBranch,
        Func<string, string, Task<IReadOnlyList<ReflogEntry>>>? loadReflog = null)
        : base(host)
    {
        _repositoryPath = repositoryPath;
        _module = module;
        _createBranch = createBranch;
        _resetCurrentBranch = resetCurrentBranch;
        _loadReflog = loadReflog ?? Reflog.LoadAsync;
    }

    /// <summary>
    ///  HEAD, then the branches.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> References { get; private set; } = [];

    /// <summary>
    ///  The reference whose reflog shows; changing it reads that reflog.
    /// </summary>
    [ObservableProperty]
    public partial string? SelectedReference { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<ReflogEntry> Entries { get; private set; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopyShaCommand), nameof(CreateBranchCommand), nameof(ResetCurrentBranchCommand))]
    public partial ReflogEntry? SelectedEntry { get; set; }

    /// <summary>
    ///  Upstream's warning that local changes could be lost by a reset.
    /// </summary>
    [ObservableProperty]
    public partial bool IsDirty { get; private set; }

    /// <summary>
    ///  True on a branch: the current branch's link shows and the reset item is enabled.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ResetCurrentBranchCommand))]
    public partial bool IsOnBranch { get; private set; }

    /// <summary>
    ///  Upstream's "current branch (name)" link.
    /// </summary>
    [ObservableProperty]
    public partial string? CurrentBranchText { get; private set; }

    /// <summary>
    ///  Copies a text to the clipboard; the window sets it, as the clipboard is the platform's.
    /// </summary>
    public Func<string, Task>? CopyText { get; set; }

    /// <summary>
    ///  Completes when the references are listed and the first reflog is shown, for tests.
    /// </summary>
    public Task Loaded => _loaded.Task;

    /// <summary>
    ///  The last read of a reflog, for tests.
    /// </summary>
    public Task EntriesLoaded { get; private set; } = Task.CompletedTask;

    private bool HasEntry => SelectedEntry is not null;

    private bool CanReset => SelectedEntry is not null && IsOnBranch;

    /// <summary>
    ///  Upstream's <c>FormReflog_Load</c>: the dirty warning, the current branch link (on a branch only) and the
    ///  references. The window calls it when it opens.
    /// </summary>
    public Task LoadAsync() => GuardAsync(async () =>
    {
        try
        {
            (bool dirty, string branch, IReadOnlyList<string> references) = await Task.Run(() =>
                (_module.IsDirtyDir(), _module.GetSelectedBranch(), Reflog.References(_module)));
            IsDirty = dirty;
            _currentBranch = branch;
            IsOnBranch = branch != DetachedHeadParser.DetachedBranch;
            CurrentBranchText = string.Format(CurrentBranchFormat, branch);
            References = references;

            // Loaded once the selected reference's entries are shown too.
            _loadingReferences = true;
            SelectedReference = references.Count > 0 ? references[0] : null;
            _loadingReferences = false;
            await (EntriesLoaded = LoadEntriesAsync(SelectedReference));
        }
        finally
        {
            _loadingReferences = false;
            _loaded.TrySetResult();
        }
    });

    // Upstream's Branches_SelectedIndexChanged.
    partial void OnSelectedReferenceChanged(string? value)
    {
        if (!_loadingReferences)
        {
            EntriesLoaded = LoadEntriesAsync(value);
        }
    }

    private Task LoadEntriesAsync(string? reference) => reference is null
        ? Task.CompletedTask
        : GuardAsync(async () =>
        {
            IReadOnlyList<ReflogEntry> entries = await _loadReflog(_repositoryPath, reference);
            if (SelectedReference == reference)
            {
                Entries = entries;
            }
        });

    [RelayCommand]
    private void ShowCurrentBranch() => SelectedReference = _currentBranch;

    [RelayCommand]
    private void ShowHead() => SelectedReference = References.Count > 0 ? References[0] : null;

    [RelayCommand(CanExecute = nameof(HasEntry))]
    private Task CopyShaAsync() => GuardAsync(async () =>
    {
        if (SelectedEntry is { } entry && CopyText is not null)
        {
            await CopyText(entry.Sha.ToString());
        }
    });

    // Upstream's createABranchOnThisCommitToolStripMenuItem_Click: the create branch window on the entry's commit.
    [RelayCommand(CanExecute = nameof(HasEntry))]
    private Task CreateBranchAsync() => GuardAsync(async () =>
    {
        if (SelectedEntry is { } entry)
        {
            await _createBranch(entry.Sha);
        }
    });

    // Upstream's resetCurrentBranchOnThisCommitToolStripMenuItem_Click: with local changes it asks first and proposes a
    // soft reset, otherwise a hard one.
    [RelayCommand(CanExecute = nameof(CanReset))]
    private Task ResetCurrentBranchAsync() => GuardAsync(async () =>
    {
        if (SelectedEntry is not { } entry)
        {
            return;
        }

        if (IsDirty && !await Host.ConfirmAsync(Host.Translate(ContinueResetWithChanges),
                Host.Translate(ContinueResetCaption), defaultIsNo: true))
        {
            return;
        }

        await _resetCurrentBranch(entry.Sha, IsDirty ? ResetMode.Soft : ResetMode.Hard);
    });
}
