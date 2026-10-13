using Avalonia.Controls;
using GitExtensions.Xplat.Core.Repository;

namespace GitExtensions.Xplat.App;

// Upstream FormCommit's status strip (commitStatusStrip) and window title.
public partial class CommitWindow
{
    private void WireStatusBar()
    {
        // Upstream reads the committer when the window opens and is activated, and when the author box is left.
        Opened += (_, _) => Run(ShowCommitterAsync);
        Activated += (_, _) => Run(ShowCommitterAsync);
        CommitAuthorBox.LostFocus += (_, _) => Run(ShowCommitterAsync);
        Opened += (_, _) => Run(ShowBranchAsync);
        CommitMessageBox.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.CaretIndexProperty || e.Property == TextBox.TextProperty)
            {
                ShowCaret();
            }
        };
    }

    private async Task ShowCommitterAsync()
    {
        CommitterText.Text = await CommitStatusBar.CommitterAsync(_repositoryPath, CommitAuthorBox.Text ?? "");
    }

    // Upstream's UpdateBranchNameDisplayAsync, which also sets the title.
    private async Task ShowBranchAsync()
    {
        CommitBranchStatus status = await CommitStatusBar.BranchAsync(_repositoryPath);
        BranchNameText.Text = status.BranchText;
        RemoteNameText.Text = status.PushTo ?? "";
        Title = CommitStatusBar.Title(status.Branch, _repositoryPath);
    }

    // Upstream's Staged_DataSourceChanged: staged files of all changed files.
    private void ShowStagedCount()
        => StagedCountText.Text = CommitStatusBar.Staged(_repository.Changes.Count(change => change.Staged),
            _repository.Changes.Count);

    // Upstream's Message_SelectionChanged.
    private void ShowCaret()
    {
        (int line, int column) = CommitStatusBar.Caret(CommitMessageBox.Text ?? "", CommitMessageBox.CaretIndex);
        CaretLineText.Text = line.ToString(System.Globalization.CultureInfo.CurrentCulture);
        CaretColumnText.Text = column.ToString(System.Globalization.CultureInfo.CurrentCulture);
    }
}
