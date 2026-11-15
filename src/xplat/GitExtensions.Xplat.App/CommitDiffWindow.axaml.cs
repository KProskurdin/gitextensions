using Avalonia.Controls;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormCommitDiff</c>: a commit's information, files and diffs in their own window, opened by a double-click
///  on a commit of the revision grid (<c>RevisionGridControl.ViewSelectedRevisions</c>). Bound to its view's
///  <see cref="Core.CommitHistory.CommitDiffViewModel"/>, whose title it takes.
/// </summary>
public partial class CommitDiffWindow : Window
{
    public CommitDiffWindow()
        : this("", "")
    {
    }

    public CommitDiffWindow(string repositoryPath, string hash)
    {
        InitializeComponent();
        WindowPlacementTracker.Attach(this, "Xplat.CommitDiffWindow");
        DataContext = CommitDiff.ViewModel;
        if (hash.Length > 0)
        {
            RevisionLoaded = CommitDiff.SetRevisionAsync(repositoryPath, hash, fileToSelect: null);
        }
    }

    /// <summary>
    ///  The read of the commit, for tests to wait on.
    /// </summary>
    public Task RevisionLoaded { get; } = Task.CompletedTask;
}
