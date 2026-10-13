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
        ClearButton.Click += (_, _) => Close(_current.ResetAll());
        ApplyButton.Click += (_, _) => Close(Filter());
    }

    // "Until" means the whole day, as a user reading the date expects. The filters this window does not show (the
    // toolbar's diff, branch and reflog choices) stay as they are.
    private RevisionFilter Filter() => _current with
    {
        Author = AuthorBox.Text?.Trim() ?? "",
        Committer = CommitterBox.Text?.Trim() ?? "",
        Message = MessageBox.Text?.Trim() ?? "",
        Since = SinceBox.SelectedDate?.Date,
        Until = UntilBox.SelectedDate?.Date.AddDays(1).AddSeconds(-1),
        PathFilter = PathBox.Text?.Trim() ?? "",
        NoMerges = NoMergesCheck.IsChecked == true,
        FirstParent = FirstParentCheck.IsChecked == true,
    };
}
