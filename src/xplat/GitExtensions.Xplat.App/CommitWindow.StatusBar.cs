using Avalonia.Controls;
using GitExtensions.Xplat.Core.Repository;

namespace GitExtensions.Xplat.App;

// Upstream FormCommit's status strip (commitStatusStrip) and window title; the texts and the links' commands are
// CommitViewModel's, the windows the links open are passed to it (CommitViewModelServices).
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
        CommitMessageBox.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.CaretIndexProperty || e.Property == TextBox.TextProperty)
            {
                // Conventional Commits' items insert at the caret.
                ViewModel.MessageCaret = CommitMessageBox.CaretIndex;
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
