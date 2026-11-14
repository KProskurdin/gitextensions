using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Presentation;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  The view model of upstream's <c>FormCompareToBranch</c> with its <c>BranchSelector</c>: a local or remote branch to
///  compare a commit with, showing how many commits each has that the other lacks. Closes its window with the branch name.
/// </summary>
public sealed partial class CompareToBranchViewModel : DialogViewModel
{
    /// <summary>
    ///  The upstream form whose texts the window shows, by their names.
    /// </summary>
    public const string Category = "FormCompareToBranch";

    private readonly IGitModule _module;
    private readonly ObjectId _commitToCompare;
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private IReadOnlyList<string>? _localBranches;
    private IReadOnlyList<string>? _remoteBranches;
    private int _countVersion;

    /// <param name="module">The repository.</param>
    /// <param name="commitToCompare">The commit the branches' commits are counted against; HEAD when zero.</param>
    public CompareToBranchViewModel(IDialogHost host, IGitModule module, ObjectId commitToCompare)
        : base(host)
    {
        _module = module;
        _commitToCompare = commitToCompare;
    }

    /// <summary>
    ///  Raised when Compare was asked without a branch: the window then focuses the branch list, as upstream.
    /// </summary>
    public event EventHandler? BranchRequired;

    /// <summary>
    ///  Upstream's "Local branch" (the remote branches come first).
    /// </summary>
    [ObservableProperty]
    public partial bool IsLocal { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<string> Branches { get; private set; } = [];

    [ObservableProperty]
    public partial string? SelectedBranch { get; set; }

    /// <summary>
    ///  The branch list's text, which may name a branch that is not listed.
    /// </summary>
    [ObservableProperty]
    public partial string? BranchText { get; set; }

    /// <summary>
    ///  Upstream's <c>lbChanges</c>: the commits between the compared commit and the branch.
    /// </summary>
    [ObservableProperty]
    public partial string Changes { get; private set; } = "";

    /// <summary>
    ///  Completes when the first branch list is shown, for tests.
    /// </summary>
    public Task Loaded => _loaded.Task;

    /// <summary>
    ///  Upstream's <c>BranchSelector.Initialize</c>: the local or the remote branches, none chosen. The window calls it when
    ///  it opens.
    /// </summary>
    public Task LoadAsync() => GuardAsync(async () =>
    {
        try
        {
            bool local = IsLocal;
            IReadOnlyList<string> branches = local
                ? _localBranches ??= await Task.Run(() => Names(RefsFilter.Heads))
                : _remoteBranches ??= await Task.Run(() => Names(RefsFilter.Remotes));
            if (local != IsLocal)
            {
                return;
            }

            Changes = "";
            Branches = branches;
            SelectedBranch = null;
            BranchText = "";
        }
        finally
        {
            _loaded.TrySetResult();
        }
    });

    partial void OnIsLocalChanged(bool value) => _ = LoadAsync();

    // Upstream's Branches_SelectedIndexChanged: the count of commits between the compared commit (or HEAD) and the branch.
    partial void OnSelectedBranchChanged(string? value)
    {
        Changes = "";
        int version = ++_countVersion;
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        Run(async () =>
        {
            string text = await Task.Run(() =>
            {
                ObjectId current = _commitToCompare.IsZero ? _module.GetCurrentCheckout() : _commitToCompare;
                return current.IsZero ? "" : _module.GetCommitCountString(current, value);
            });
            if (version == _countVersion)
            {
                Changes = text;
            }
        });
    }

    // Upstream's btnCompare_Click: closes with the branch, or asks for one.
    [RelayCommand]
    private void Compare()
    {
        string branch = (SelectedBranch ?? BranchText ?? "").Trim();
        if (branch.Length > 0)
        {
            Host.Close(branch);
            return;
        }

        BranchRequired?.Invoke(this, EventArgs.Empty);
    }

    private IReadOnlyList<string> Names(RefsFilter filter) => [.. _module.GetRefs(filter).Select(gitRef => gitRef.Name)];
}
