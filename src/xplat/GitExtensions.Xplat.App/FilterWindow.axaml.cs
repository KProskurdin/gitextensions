using Avalonia.Controls;
using GitExtensions.Xplat.Core.CommitHistory;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormRevisionFilter</c>: each filter turned on by its check box (since, until, author, committer,
///  message, diff content, ignore case, limit, path, branches), and the branch and history options (current branch only,
///  reflog, first parent, hide merge commits, simplify by decoration, full history, simplify merges), with upstream's
///  <c>UpdateFilters</c> rules. Closes with the new filter, or null when cancelled.
/// </summary>
public partial class FilterWindow : Window
{
    private readonly RevisionFilter _current;
    private readonly int _defaultLimit;

    /// <summary>
    ///  The design-time constructor (<see cref="DesignTime"/>): the window's XAML only.
    /// </summary>
    [Obsolete(DesignTime.ConstructorOnly)]
#pragma warning disable CS8618 // The fields the window's data fills stay unset: no code of the window runs.
    public FilterWindow() => InitializeComponent();
#pragma warning restore CS8618

    /// <param name="defaultLimit">Upstream's <c>CommitsLimitDefault</c> (<c>MaxRevisionGraphCommits</c>), the limit shown
    ///  while none is set.</param>
    public FilterWindow(RevisionFilter current, int defaultLimit = 0)
    {
        _current = current;
        _defaultLimit = defaultLimit;
        InitializeComponent();
        InitialFocus.Attach(this);

        // Upstream's OnLoad: a date not set shows today.
        SinceCheck.IsChecked = current.Since is not null;
        SinceBox.SelectedDate = current.Since?.Date ?? DateTime.Today;
        UntilCheck.IsChecked = current.Until is not null;
        UntilBox.SelectedDate = current.Until?.Date ?? DateTime.Today;
        Show(AuthorCheck, AuthorBox, current.Author);
        Show(CommitterCheck, CommitterBox, current.Committer);
        Show(MessageCheck, MessageBox, current.Message);
        Show(DiffContentCheck, DiffContentBox, current.DiffContent);
        IgnoreCaseCheck.IsChecked = current.IgnoreCase;
        CommitsLimitCheck.IsChecked = current.CommitsLimit > 0;
        CommitsLimitBox.Value = current.CommitsLimit > 0 ? current.CommitsLimit : defaultLimit;
        Show(PathFilterCheck, PathBox, current.PathFilter);
        BranchFilterCheck.IsChecked = current.ShowFilteredBranches;
        BranchFilterBox.Text = current.BranchFilter;
        CurrentBranchOnlyCheck.IsChecked = current.CurrentBranchOnly;
        ReflogCheck.IsChecked = current.ShowReflog;
        FirstParentCheck.IsChecked = current.FirstParent;
        NoMergesCheck.IsChecked = current.NoMerges;
        SimplifyByDecorationCheck.IsChecked = current.SimplifyByDecoration;
        FullHistoryCheck.IsChecked = current.FullHistory;
        SimplifyMergesCheck.IsChecked = current.SimplifyMerges;
        UpdateFilters();

        foreach (CheckBox check in new[]
                 {
                     SinceCheck, UntilCheck, AuthorCheck, CommitterCheck, MessageCheck, DiffContentCheck,
                     PathFilterCheck, BranchFilterCheck, CurrentBranchOnlyCheck, ReflogCheck
                 })
        {
            check.IsCheckedChanged += (_, _) => UpdateFilters();
        }

        // Upstream's option_CheckedChanged: an unchecked limit shows the default again.
        CommitsLimitCheck.IsCheckedChanged += (_, _) =>
        {
            UpdateFilters();
            if (CommitsLimitCheck.IsChecked != true)
            {
                CommitsLimitBox.Value = _defaultLimit;
            }
        };
        CancelButton.Click += (_, _) => Close(null);
        ApplyButton.Click += (_, _) => Close(Filter());
        return;

        static void Show(CheckBox check, TextBox box, string value)
        {
            check.IsChecked = value.Length > 0;
            box.Text = value;
        }
    }

    // Upstream's UpdateFilters.
    private void UpdateFilters()
    {
        SinceBox.IsEnabled = SinceCheck.IsChecked == true;
        UntilBox.IsEnabled = UntilCheck.IsChecked == true;
        AuthorBox.IsEnabled = AuthorCheck.IsChecked == true;
        CommitterBox.IsEnabled = CommitterCheck.IsChecked == true;
        MessageBox.IsEnabled = MessageCheck.IsChecked == true;
        DiffContentBox.IsEnabled = DiffContentCheck.IsChecked == true;
        IgnoreCaseCheck.IsEnabled = AuthorBox.IsEnabled || CommitterBox.IsEnabled || MessageBox.IsEnabled
                                    || DiffContentBox.IsEnabled;
        CommitsLimitBox.IsEnabled = CommitsLimitCheck.IsChecked == true;
        PathBox.IsEnabled = PathFilterCheck.IsChecked == true;
        CurrentBranchOnlyCheck.IsEnabled = ReflogCheck.IsChecked != true;
        BranchFilterCheck.IsEnabled = CurrentBranchOnlyCheck.IsChecked != true && ReflogCheck.IsChecked != true;
        BranchFilterBox.IsEnabled = BranchFilterCheck.IsChecked == true;
    }

    // Upstream's OkClick. "Until" means the whole day, as a user reading the date expects.
    private RevisionFilter Filter() => _current with
    {
        Since = SinceCheck.IsChecked == true ? SinceBox.SelectedDate?.Date : null,
        Until = UntilCheck.IsChecked == true ? UntilBox.SelectedDate?.Date.AddDays(1).AddSeconds(-1) : null,
        Author = Value(AuthorCheck, AuthorBox),
        Committer = Value(CommitterCheck, CommitterBox),
        Message = Value(MessageCheck, MessageBox),
        DiffContent = Value(DiffContentCheck, DiffContentBox),
        IgnoreCase = IgnoreCaseCheck.IsChecked == true,
        CommitsLimit = CommitsLimitCheck.IsChecked == true ? (int)(CommitsLimitBox.Value ?? 0) : 0,
        PathFilter = Value(PathFilterCheck, PathBox),
        ShowFilteredBranches = BranchFilterCheck.IsChecked == true,
        BranchFilter = BranchFilterBox.Text?.Trim() ?? "",
        CurrentBranchOnly = CurrentBranchOnlyCheck.IsChecked == true,
        ShowReflog = ReflogCheck.IsChecked == true,
        FirstParent = FirstParentCheck.IsChecked == true,
        NoMerges = NoMergesCheck.IsChecked == true,
        SimplifyByDecoration = SimplifyByDecorationCheck.IsChecked == true,
        FullHistory = FullHistoryCheck.IsChecked == true,
        SimplifyMerges = SimplifyMergesCheck.IsChecked == true,
    };

    private static string Value(CheckBox check, TextBox box) => check.IsChecked == true ? box.Text?.Trim() ?? "" : "";
}
