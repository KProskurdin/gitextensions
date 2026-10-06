using Avalonia.Controls;
using GitExtensions.Xplat.Core.Operations;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Asks what to pull or fetch: the new shell's version of upstream <c>FormPull</c>. Closes with the request, or null when
///  cancelled.
/// </summary>
public partial class PullWindow : Window
{
    public PullWindow(IReadOnlyList<string> remotes, string defaultRemote, bool rebaseByDefault)
    {
        InitializeComponent();
        RemoteBox.ItemsSource = remotes;
        RemoteBox.SelectedItem = remotes.Contains(defaultRemote) ? defaultRemote : remotes.Count > 0 ? remotes[0] : null;
        RebaseRadio.IsChecked = rebaseByDefault;
        MergeRadio.IsChecked = !rebaseByDefault;
        CancelButton.Click += (_, _) => Close(null);
        PullButton.Click += (_, _) => Close(Request());
        FetchRadio.IsCheckedChanged += (_, _) => UpdateOptions();
        RemoteBox.SelectionChanged += (_, _) => UpdateOptions();
        UpdateOptions();
    }

    private void UpdateOptions()
    {
        bool fetchOnly = FetchRadio.IsChecked == true;
        PruneCheck.IsEnabled = fetchOnly;
        AutoStashCheck.IsEnabled = !fetchOnly;
        PullButton.Content = fetchOnly ? "Fetch" : "Pull";
        PullButton.IsEnabled = RemoteBox.SelectedItem is not null;
    }

    private PullRequest Request()
    {
        PullAction action = FetchRadio.IsChecked == true ? PullAction.FetchOnly
            : RebaseRadio.IsChecked == true ? PullAction.Rebase
            : PullAction.Merge;
        return new PullRequest(RemoteBox.SelectedItem as string ?? "", RemoteBranchBox.Text?.Trim() ?? "", action,
            Prune: action == PullAction.FetchOnly && PruneCheck.IsChecked == true,
            AutoStash: action != PullAction.FetchOnly && AutoStashCheck.IsChecked == true);
    }
}
