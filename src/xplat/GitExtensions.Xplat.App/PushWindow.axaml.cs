using Avalonia.Controls;
using GitCommands.Git;
using GitExtensions.Xplat.Core.Operations;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Asks what to push and where: the new shell's version of upstream <c>FormPush</c> (branch push). Closes with the request,
///  or null when cancelled.
/// </summary>
public partial class PushWindow : Window
{
    public PushWindow(IReadOnlyList<string> remotes, string defaultRemote, IReadOnlyList<string> localBranches, string currentBranch,
        bool hasUpstream)
    {
        InitializeComponent();
        RemoteBox.ItemsSource = remotes;
        RemoteBox.SelectedItem = remotes.Contains(defaultRemote) ? defaultRemote : remotes.Count > 0 ? remotes[0] : null;
        LocalBranchBox.ItemsSource = localBranches;
        LocalBranchBox.SelectedItem = localBranches.Contains(currentBranch) ? currentBranch : localBranches.Count > 0 ? localBranches[0] : null;

        // Like upstream, a branch without an upstream is pushed with tracking, so pull and push work without a dialog next time.
        TrackCheck.IsChecked = !hasUpstream;
        CancelButton.Click += (_, _) => Close(null);
        PushButton.Click += (_, _) => Close(Request());
        RemoteBox.SelectionChanged += (_, _) => PushButton.IsEnabled = RemoteBox.SelectedItem is not null;
        PushButton.IsEnabled = RemoteBox.SelectedItem is not null;
    }

    private PushRequest Request()
    {
        ForcePushOptions force = ForceWithLeaseRadio.IsChecked == true ? ForcePushOptions.ForceWithLease
            : ForceRadio.IsChecked == true ? ForcePushOptions.Force
            : ForcePushOptions.DoNotForce;
        return new PushRequest(RemoteBox.SelectedItem as string ?? "", LocalBranchBox.SelectedItem as string ?? "",
            RemoteBranchBox.Text?.Trim() ?? "", force, TrackCheck.IsChecked == true);
    }
}
