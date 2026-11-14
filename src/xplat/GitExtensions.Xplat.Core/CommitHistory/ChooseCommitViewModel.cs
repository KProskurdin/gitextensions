using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Presentation;

namespace GitExtensions.Xplat.Core.CommitHistory;

/// <summary>
///  The view model of upstream's <c>FormChooseCommit</c>: a revision grid to pick one commit, with "Go to commit..." and
///  links to the selected commit's parents. Closes its window with the chosen <see cref="CommitRow"/>.
/// </summary>
public sealed partial class ChooseCommitViewModel : DialogViewModel
{
    /// <summary>
    ///  The upstream form whose texts the window shows, by their names.
    /// </summary>
    public const string Category = "FormChooseCommit";

    private const int ShortHashLength = 8;

    private readonly IGitModule _module;
    private readonly string _repositoryPath;
    private readonly string? _preselect;
    private readonly RevisionFilter _filter;
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <param name="module">The repository whose commits are listed.</param>
    /// <param name="repositoryPath">The repository's working directory, as the caller names it.</param>
    /// <param name="history">Reads the grid's commits.</param>
    /// <param name="preselect">The commit selected at first (upstream's <c>preselectCommit</c>), or null.</param>
    /// <param name="showCurrentBranchOnly">Upstream's <c>showCurrentBranchOnly</c>: only the current branch's commits.</param>
    /// <param name="lastRevisionToDisplayHash">Upstream's <c>lastRevisionToDisplayHash</c>: the history stops at this
    /// commit; null for the whole history.</param>
    public ChooseCommitViewModel(IDialogHost host, IGitModule module, string repositoryPath, ICommitHistory history,
        string? preselect, bool showCurrentBranchOnly = false, string? lastRevisionToDisplayHash = null)
        : base(host)
    {
        _module = module;
        _repositoryPath = repositoryPath;
        _preselect = preselect;
        _filter = new RevisionFilter(CurrentBranchOnly: showCurrentBranchOnly,
            LastRevisionToDisplayHash: lastRevisionToDisplayHash ?? "");
        Commits = new CommitListViewModel(history);
    }

    /// <summary>
    ///  The grid's commits.
    /// </summary>
    public CommitListViewModel Commits { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Parent1), nameof(Parent2), nameof(HasParents), nameof(Parent1Text),
        nameof(Parent2Text))]
    [NotifyCanExecuteChangedFor(nameof(ChooseCommand))]
    public partial int SelectedIndex { get; set; } = -1;

    /// <summary>
    ///  Opens upstream's FormGoToCommit and returns the commit it resolved (zero for none), or null when cancelled. The
    ///  window sets it, as it opens another window.
    /// </summary>
    public Func<Task<ObjectId?>>? OpenGoToCommit { get; set; }

    public CommitRow? SelectedRow => SelectedIndex >= 0 && SelectedIndex < Commits.VisibleRows.Count
        ? Commits.VisibleRows[SelectedIndex].Row
        : null;

    /// <summary>
    ///  Upstream's <c>revisionGrid_SelectionChanged</c>: the parents' links show when the commit has parents.
    /// </summary>
    public bool HasParents => Parent1 is not null;

    public string? Parent1 => SelectedRow?.ParentHashes is [{ } parent, ..] ? parent : null;

    public string? Parent2 => SelectedRow?.ParentHashes is [_, { } parent, ..] ? parent : null;

    public string? Parent1Text => Short(Parent1);

    public string? Parent2Text => Short(Parent2);

    /// <summary>
    ///  Completes when the commits are listed, for tests.
    /// </summary>
    public Task Loaded => _loaded.Task;

    private bool CanChoose => SelectedRow is not null;

    /// <summary>
    ///  Upstream's <c>OnLoad</c> and its constructor's preselected commit. The window calls it when it opens.
    /// </summary>
    public Task LoadAsync() => GuardAsync(async () =>
    {
        await Commits.ApplyFilterAsync(_filter);
        await Commits.OpenAsync(_repositoryPath);

        // An artificial commit is no revision for git to resolve.
        string? hash = string.IsNullOrEmpty(_preselect)
            ? null
            : ObjectId.TryParse(_preselect, out ObjectId given) && given.IsArtificial
                ? _preselect
                : await Task.Run(() => CommitHistory.GoToCommit.Resolve(_module, _preselect)) is { IsZero: false } id
                    ? id.ToString()
                    : null;
        if (hash is null || !Select(hash))
        {
            SelectedIndex = Commits.VisibleRows.Count > 0 ? 0 : -1;
        }

        _loaded.TrySetResult();
    });

    // Upstream's btnOK_Click and revisionGrid_DoubleClickRevision.
    [RelayCommand(CanExecute = nameof(CanChoose))]
    private void Choose()
    {
        if (SelectedRow is { } row)
        {
            Host.Close(row);
        }
    }

    // Upstream's buttonGotoCommit_Click: the grid's GotoCommitExecute.
    [RelayCommand]
    private Task GoToCommitAsync() => GuardAsync(async () =>
    {
        if (OpenGoToCommit is null || await OpenGoToCommit() is not { } commit)
        {
            return;
        }

        if (commit.IsZero)
        {
            Host.ShowError(new InvalidOperationException(Host.Translate(CommitHistory.GoToCommit.NoRevisionFound)));
        }
        else
        {
            SelectOrTell(commit.ToString());
        }
    });

    // Upstream's linkLabelParent_LinkClicked: the commit, or upstream's RevisionFilteredInGrid message.
    [RelayCommand]
    private void SelectOrTell(string? hash)
    {
        if (hash is not null && !Select(hash))
        {
            Host.ShowError(new InvalidOperationException(string.Format(
                Host.Translate(CommitHistory.GoToCommit.RevisionNotVisible), hash[..Math.Min(hash.Length, ShortHashLength)])));
        }
    }

    // Upstream's SetSelectedRevision: false when the grid does not show the commit.
    private bool Select(string hash)
    {
        if (Commits.IndexOfHash(hash) is not { } index)
        {
            return false;
        }

        SelectedIndex = index;
        return true;
    }

    private static string? Short(string? hash) => hash is null ? null : ObjectId.Parse(hash).ToShortString();
}
