using Avalonia.Controls;
using GitCommands;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormCompareToBranch</c> with its <c>BranchSelector</c>: a local or remote branch to compare a commit with,
///  showing how many commits each has that the other lacks. Closes with the branch name, or null when closed without one.
/// </summary>
public partial class CompareToBranchWindow : Window
{
    private readonly GitModule _module;
    private readonly ObjectId _commitToCompare;
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private IReadOnlyList<string>? _localBranches;
    private IReadOnlyList<string>? _remoteBranches;
    private int _countVersion;

    public CompareToBranchWindow()
        : this("", default)
    {
    }

    /// <param name="repositoryPath">The repository.</param>
    /// <param name="commitToCompare">The commit the branches' commits are counted against; HEAD when zero.</param>
    public CompareToBranchWindow(string repositoryPath, ObjectId commitToCompare)
    {
        _module = GitModules.Open(repositoryPath);
        _commitToCompare = commitToCompare;
        InitializeComponent();
        UpstreamTranslation.Apply(this, "FormCompareToBranch");
        InitialFocus.Attach(this, () => Branches);
        Remotebranch.IsChecked = true;
        LocalBranch.IsCheckedChanged += (_, _) => Run(LoadBranchesAsync);
        Branches.SelectionChanged += (_, _) => ShowChanges();
        btnCompare.Click += (_, _) => Compare();
        Opened += (_, _) => Run(LoadBranchesAsync);
    }

    /// <summary>
    ///  Completes when the first branch list is shown, for tests.
    /// </summary>
    public Task BranchesLoaded => _loaded.Task;

    // Upstream's BranchSelector.Initialize: the local or the remote branches, none chosen.
    private async Task LoadBranchesAsync()
    {
        try
        {
            bool local = LocalBranch.IsChecked == true;
            IReadOnlyList<string> branches = local
                ? _localBranches ??= await Task.Run(() => Names(RefsFilter.Heads))
                : _remoteBranches ??= await Task.Run(() => Names(RefsFilter.Remotes));
            if (local != (LocalBranch.IsChecked == true))
            {
                return;
            }

            lbChanges.Text = "";
            Branches.ItemsSource = branches;
            Branches.SelectedItem = null;
            Branches.Text = "";
        }
        finally
        {
            _loaded.TrySetResult();
        }
    }

    private IReadOnlyList<string> Names(RefsFilter filter) => [.. _module.GetRefs(filter).Select(gitRef => gitRef.Name)];

    // Upstream's Branches_SelectedIndexChanged: the count of commits between the compared commit (or HEAD) and the branch.
    private void ShowChanges()
    {
        lbChanges.Text = "";
        if (Branches.SelectedItem is not string branch || string.IsNullOrWhiteSpace(branch))
        {
            return;
        }

        int version = ++_countVersion;
        Run(async () =>
        {
            string text = await Task.Run(() =>
            {
                ObjectId current = _commitToCompare.IsZero ? _module.GetCurrentCheckout() : _commitToCompare;
                return current.IsZero ? "" : _module.GetCommitCountString(current, branch);
            });
            if (version == _countVersion)
            {
                lbChanges.Text = text;
            }
        });
    }

    // Upstream's btnCompare_Click: closes with the branch, or keeps the focus on the list without one.
    private void Compare()
    {
        string branch = (Branches.SelectedItem as string ?? Branches.Text ?? "").Trim();
        if (branch.Length > 0)
        {
            Close(branch);
            return;
        }

        Branches.Focus();
    }

    private void Run(Func<Task> action)
        => UiActions.Run(action, ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));
}
