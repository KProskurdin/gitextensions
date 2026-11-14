using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitCommands;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Presentation;

namespace GitExtensions.Xplat.Core.Diff;

/// <summary>
///  The view model of upstream's <c>FormDiff</c> (rules in <see cref="RevisionDiff"/>): the files changed from a BASE
///  revision to a compared one. Either side can be another branch or commit (windows the view opens), the two can be
///  swapped, BASE can be their merge base, and the directory diff tool can open them. The file list and the selected
///  file's diff are the view's.
/// </summary>
public sealed partial class RevisionDiffViewModel : DialogViewModel
{
    /// <summary>
    ///  The upstream form whose texts the window shows, by their names.
    /// </summary>
    public const string Category = "FormDiff";

    private readonly GitModule _module;
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private ObjectId? _mergeBase;
    private int _populate;

    /// <param name="module">The repository.</param>
    /// <param name="first">The BASE revision.</param>
    /// <param name="second">The revision compared to BASE.</param>
    /// <param name="firstDisplay">What BASE is shown as (a subject or a branch name).</param>
    /// <param name="secondDisplay">What the compared revision is shown as.</param>
    public RevisionDiffViewModel(IDialogHost host, GitModule module, ObjectId first, ObjectId second,
        string firstDisplay, string secondDisplay)
        : base(host)
    {
        _module = module;
        First = first;
        Second = second;
        FirstDisplay = firstDisplay;
        SecondDisplay = secondDisplay;
        CompareToMergeBaseText = Host.Translate(RevisionDiff.CompareToMergeBase);
    }

    /// <summary>
    ///  The BASE revision; null when a branch git does not know was picked.
    /// </summary>
    [ObservableProperty]
    public partial ObjectId? First { get; private set; }

    /// <summary>
    ///  The revision compared to BASE.
    /// </summary>
    [ObservableProperty]
    public partial ObjectId? Second { get; private set; }

    [ObservableProperty]
    public partial string? FirstDisplay { get; private set; }

    [ObservableProperty]
    public partial string? SecondDisplay { get; private set; }

    /// <summary>
    ///  Upstream's "Compare to merge base", with the merge base once it is known.
    /// </summary>
    [ObservableProperty]
    public partial string CompareToMergeBaseText { get; private set; }

    [ObservableProperty]
    public partial bool CanCompareToMergeBase { get; private set; }

    [ObservableProperty]
    public partial bool CompareToMergeBase { get; set; }

    /// <summary>
    ///  The directory diff tool opens the revisions unless BASE is the work tree or the index.
    /// </summary>
    [ObservableProperty]
    public partial bool CanOpenDirectoryDiff { get; private set; }

    /// <summary>
    ///  The changed files; null while they are read.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<RevisionDiffFile>? Files { get; private set; }

    /// <summary>
    ///  The revision the files are compared from: BASE, or its merge base with the compared one.
    /// </summary>
    public ObjectId? ShownFrom => CompareToMergeBase ? _mergeBase : First;

    /// <summary>
    ///  Completes when the merge base is known and the first file list is shown, for tests.
    /// </summary>
    public Task Loaded => _loaded.Task;

    /// <summary>
    ///  Completes when the last file list asked for is shown, for tests.
    /// </summary>
    public Task FilesShown { get; private set; } = Task.CompletedTask;

    /// <summary>
    ///  Picks a branch from the side's commit (upstream's FormCompareToBranch); the window sets it, as it opens a window.
    /// </summary>
    public Func<ObjectId?, Task<string?>>? PickBranch { get; set; }

    /// <summary>
    ///  Picks a commit from the side's commit, returning its hash and subject (upstream's FormChooseCommit); the window
    ///  sets it.
    /// </summary>
    public Func<ObjectId?, Task<(string Hash, string Subject)?>>? PickCommit { get; set; }

    /// <summary>
    ///  Upstream's constructor (the merge base, which later picks do not change) and <c>Load</c>. The window calls it
    ///  when it opens.
    /// </summary>
    public Task LoadAsync() => GuardAsync(async () =>
    {
        try
        {
            ObjectId? first = First;
            ObjectId? second = Second;
            _mergeBase = first is { } a && second is { } b
                ? await Task.Run(() => RevisionDiff.MergeBase(_module, a, b))
                : null;
            CompareToMergeBaseText = $"{Host.Translate(RevisionDiff.CompareToMergeBase)} ({_mergeBase?.ToShortString()})";
            CanCompareToMergeBase = _mergeBase is not null;
            await (FilesShown = PopulateDiffFilesAsync());
        }
        finally
        {
            _loaded.TrySetResult();
        }
    });

    /// <summary>
    ///  Upstream's <c>PopulateDiffFiles</c>: the files read again, as after a change the menu made.
    /// </summary>
    public void Refresh() => FilesShown = PopulateDiffFilesAsync();

    partial void OnCompareToMergeBaseChanged(bool value)
    {
        OnPropertyChanged(nameof(ShownFrom));
        Refresh();
    }

    // The labels, the directory diff tool (not from the work tree), and the files from BASE (or the merge base) to the
    // compared revision; the task completes when the files are shown.
    private Task PopulateDiffFilesAsync()
    {
        CanOpenDirectoryDiff = First is { } first && RevisionDiff.CanOpenDirectoryDiff(first);
        int populate = ++_populate;
        Files = null;
        return ShownFrom is { } from && Second is { } to ? ShowFilesAsync(populate, from, to) : Task.CompletedTask;
    }

    private async Task ShowFilesAsync(int populate, ObjectId from, ObjectId to)
    {
        try
        {
            IReadOnlyList<RevisionDiffFile> files = await Task.Run(() => RevisionDiff.Files(_module, from, to));
            if (populate == _populate)
            {
                Files = files;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (populate == _populate)
            {
                Host.ShowError(ex);
            }
        }
    }

    private void SetSide(bool first, ObjectId? id, string display)
    {
        if (first)
        {
            First = id;
            FirstDisplay = display;
        }
        else
        {
            Second = id;
            SecondDisplay = display;
        }

        OnPropertyChanged(nameof(ShownFrom));
        Refresh();
    }

    [RelayCommand]
    private void Swap()
    {
        (Second, First) = (First, Second);
        (SecondDisplay, FirstDisplay) = (FirstDisplay, SecondDisplay);
        OnPropertyChanged(nameof(ShownFrom));
        Refresh();
    }

    // Upstream's btnCompareDirectoriesWithDiffTool_Clicked.
    [RelayCommand]
    private Task OpenDirectoryDiffAsync() => GuardAsync(async () =>
    {
        if (ShownFrom is { } fromId && Second is { } toId)
        {
            await Task.Run(() => _module.OpenWithDifftoolDirDiff(fromId.ToString(), toId.ToString(), customTool: null));
        }
    });

    // Upstream's PickAnotherBranch: the compare to branch window from the side's commit; a name git does not know leaves
    // the side empty.
    [RelayCommand]
    private Task PickFirstBranchAsync() => GuardAsync(() => PickAnotherBranchAsync(first: true));

    [RelayCommand]
    private Task PickSecondBranchAsync() => GuardAsync(() => PickAnotherBranchAsync(first: false));

    // Upstream's PickAnotherCommit: the choose commit window from the side's commit.
    [RelayCommand]
    private Task PickFirstCommitAsync() => GuardAsync(() => PickAnotherCommitAsync(first: true));

    [RelayCommand]
    private Task PickSecondCommitAsync() => GuardAsync(() => PickAnotherCommitAsync(first: false));

    private async Task PickAnotherBranchAsync(bool first)
    {
        if (PickBranch is null || await PickBranch(first ? First : Second) is not { } branch)
        {
            return;
        }

        ObjectId id = await Task.Run(() => _module.RevParse(branch));
        SetSide(first, id.IsZero ? null : id, branch);
    }

    private async Task PickAnotherCommitAsync(bool first)
    {
        if (PickCommit is null || await PickCommit(first ? First : Second) is not { } commit)
        {
            return;
        }

        SetSide(first, ObjectId.TryParse(commit.Hash, out ObjectId id) ? id : null, commit.Subject);
    }
}
