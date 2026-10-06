using Avalonia.Controls;
using GitExtensions.Xplat.Core.CommitHistory;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Edits the advanced revision filter: the new shell's version of upstream <c>FormRevisionFilter</c>. Closes with the new
///  filter, or null when cancelled. The branch choice is kept as it was; the toolbar sets it.
/// </summary>
public partial class FilterWindow : Window
{
    private readonly RevisionFilter _current;

    public FilterWindow(RevisionFilter current)
    {
        _current = current;
        InitializeComponent();
        AuthorBox.Text = current.Author;
        CommitterBox.Text = current.Committer;
        MessageBox.Text = current.Message;
        SinceBox.SelectedDate = current.Since;
        UntilBox.SelectedDate = current.Until;
        PathBox.Text = current.PathFilter;
        NoMergesCheck.IsChecked = current.NoMerges;
        FirstParentCheck.IsChecked = current.FirstParent;
        CancelButton.Click += (_, _) => Close(null);
        ClearButton.Click += (_, _) => Close(new RevisionFilter(CurrentBranchOnly: _current.CurrentBranchOnly));
        ApplyButton.Click += (_, _) => Close(Filter());
    }

    // "Until" means the whole day, as a user reading the date expects.
    private RevisionFilter Filter() => new(
        _current.CurrentBranchOnly,
        AuthorBox.Text?.Trim() ?? "",
        CommitterBox.Text?.Trim() ?? "",
        MessageBox.Text?.Trim() ?? "",
        SinceBox.SelectedDate?.Date,
        UntilBox.SelectedDate?.Date.AddDays(1).AddSeconds(-1),
        PathBox.Text?.Trim() ?? "",
        NoMergesCheck.IsChecked == true,
        FirstParentCheck.IsChecked == true);
}
