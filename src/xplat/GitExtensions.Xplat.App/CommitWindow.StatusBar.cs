using Avalonia.Controls;
using GitExtensions.Xplat.Core.Repository;

namespace GitExtensions.Xplat.App;

// Upstream FormCommit's status strip (commitStatusStrip) and window title; the texts are CommitViewModel's.
public partial class CommitWindow
{
    private const string GitConfigSettingsPage = "GitConfigSettingsPage";

    private void WireStatusBar()
    {
        // Upstream reads the committer when the window opens and is activated, and when the author box is left.
        Opened += (_, _) => _ = ViewModel.ShowCommitterAsync();
        Activated += (_, _) => _ = ViewModel.ShowCommitterAsync();
        CommitAuthorBox.LostFocus += (_, _) => _ = ViewModel.ShowCommitterAsync();
        Opened += (_, _) => _ = ViewModel.ShowBranchAsync();

        // Upstream's commitAuthorStatus: the Git config settings page, where user.name and user.email are set.
        CommitterLink.Click += (_, _) => Run(async () =>
        {
            if (_showSettings is not null)
            {
                await _showSettings(this, GitConfigSettingsPage);
                await ViewModel.ShowCommitterAsync();
            }
        });

        // Upstream's remoteNameLabel: the remotes window on the branch's pull behavior, then the branch read again.
        RemoteNameLink.Click += (_, _) => Run(async () =>
        {
            CommitBranchStatus status = await CommitStatusBar.BranchAsync(_repositoryPath);
            await new RemotesWindow(_repositoryPath, _preferences,
                remote => _actions.FetchAsync(_repositoryPath, remote, prune: false),
                preselectLocal: status.Branch.Length > 0 ? status.Branch : null).ShowDialog(this);
            await ViewModel.ShowBranchAsync();
        });
        CommitMessageBox.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.CaretIndexProperty || e.Property == TextBox.TextProperty)
            {
                ShowCaret();
            }
        };
    }

    // Upstream's Message_SelectionChanged: the caret's line and column, a view concern.
    private void ShowCaret()
    {
        (int line, int column) = CommitStatusBar.Caret(CommitMessageBox.Text ?? "", CommitMessageBox.CaretIndex);
        CaretLineText.Text = line.ToString(System.Globalization.CultureInfo.CurrentCulture);
        CaretColumnText.Text = column.ToString(System.Globalization.CultureInfo.CurrentCulture);
    }
}
