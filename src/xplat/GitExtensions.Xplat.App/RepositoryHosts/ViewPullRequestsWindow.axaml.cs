using Avalonia.Controls;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.RepositoryHosts;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App.RepositoryHosts;

/// <summary>
///  The new shell's version of upstream's <c>ViewPullRequestsForm</c>, bound to <see cref="ViewPullRequestsViewModel"/>.
///  Upstream shows the discussion as HTML; here it is a list of entries. The diff viewer and scrolling the discussion to
///  its last entry stay here.
/// </summary>
public partial class ViewPullRequestsWindow : Window
{
    /// <summary>
    ///  The design-time constructor (<see cref="DesignTime"/>): the window's XAML only.
    /// </summary>
    [Obsolete(DesignTime.ConstructorOnly)]
#pragma warning disable CS8618 // The fields the window's data fills stay unset: no code of the window runs.
    public ViewPullRequestsWindow() => InitializeComponent();
#pragma warning restore CS8618

    public ViewPullRequestsWindow(IRepositoryHostPlugin gitHoster, IGitModule module, RepositoryOperationsViewModel actions)
    {
        ViewModel = new ViewPullRequestsViewModel(new WindowDialogHost(this), RepositoryHostMessages.For(this), gitHoster,
            module, actions);
        InitializeComponent();
        UpstreamTranslation.Apply(this, ViewPullRequestsViewModel.Category);
        DataContext = ViewModel;

        // View concerns: the hosted repositories show their display data, the viewer shows the selected file's patch, and
        // the discussion scrolls to its last entry.
        _selectHostedRepoCB.DisplayMemberBinding = new Avalonia.Data.Binding(nameof(IHostedRemote.DisplayData));
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ViewPullRequestsViewModel.ShownPatch))
            {
                if (ViewModel.ShownPatch is { } patch)
                {
                    _diffViewer.ShowPatch(patch.Name, patch.Patch);
                }
                else
                {
                    _diffViewer.Clear();
                }
            }
        };
        ViewModel.DiscussionShown += (_, _) => DiscussionScroll.ScrollToEnd();
        Opened += (_, _) => _ = ViewModel.LoadAsync();
    }

    public ViewPullRequestsViewModel ViewModel { get; }

    /// <summary>
    ///  The pull requests shown, for tests.
    /// </summary>
    public IReadOnlyList<PullRequestRow> PullRequests => ViewModel.PullRequests;
}
