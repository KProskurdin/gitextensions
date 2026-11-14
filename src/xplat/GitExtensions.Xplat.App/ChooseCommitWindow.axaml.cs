using System.ComponentModel;
using Avalonia.Controls;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormChooseCommit</c>, bound to <see cref="ChooseCommitViewModel"/>: a revision grid to pick one commit (OK,
///  or a double-click), with "Go to commit..." and links to the selected commit's parents. Closes with the chosen row, or
///  null.
/// </summary>
public partial class ChooseCommitWindow : Window
{
    public ChooseCommitWindow()
        : this("", null, AppServices.Preferences)
    {
    }

    /// <param name="repositoryPath">The repository whose commits are listed.</param>
    /// <param name="preselect">The commit selected at first (upstream's <c>preselectCommit</c>), or null.</param>
    /// <param name="preferences">The grid's columns and dates, as the browse window shows them.</param>
    /// <param name="showCurrentBranchOnly">Upstream's <c>showCurrentBranchOnly</c>: only the current branch's commits.</param>
    /// <param name="lastRevisionToDisplayHash">
    ///  Upstream's <c>lastRevisionToDisplayHash</c>: the history stops at this commit; null for the whole history.
    /// </param>
    /// <param name="showArtificial">
    ///  Upstream's <c>showArtificial</c>: the working directory and index rows too, where the grid setting shows them.
    /// </param>
    public ChooseCommitWindow(string repositoryPath, string? preselect, IAppPreferences preferences,
        bool showCurrentBranchOnly = false, string? lastRevisionToDisplayHash = null, bool showArtificial = false)
    {
        ViewModel = new ChooseCommitViewModel(new WindowDialogHost(this), GitModules.Open(repositoryPath),
            repositoryPath, new GitCommitHistory(
                () => new CommitDateStyle(preferences.RelativeDate, preferences.ShowAuthorDate),
                () => preferences.RevisionSortOrder,
                showArtificialCommits: () => showArtificial && preferences.RevisionGraphShowArtificialCommits,
                gridView: () => preferences.GridViewOptions, showSessionRefs: () => preferences.ShowSessionRefs),
            preselect, showCurrentBranchOnly, lastRevisionToDisplayHash)
        {
            // The window FormGoToCommit opens over this one.
            OpenGoToCommit = () => new GoToCommitWindow(repositoryPath).ShowDialog<ObjectId?>(this),
        };
        DataContext = ViewModel;
        InitializeComponent();
        UpstreamTranslation.Apply(this, ChooseCommitViewModel.Category);

        // View concerns: the grid's columns as the settings say, the selected commit in view, and the double-click.
        RevisionGridColumns.ShowAsSettingsSay(this, revisionGrid, preferences, ViewModel.Commits);
        ViewModel.PropertyChanged += OnViewModelChanged;
        revisionGrid.DoubleTapped += (_, _) => ViewModel.ChooseCommand.Execute(null);
        Opened += (_, _) => _ = ViewModel.LoadAsync();
    }

    public ChooseCommitViewModel ViewModel { get; }

    /// <summary>
    ///  Completes when the commits are listed, for tests.
    /// </summary>
    public Task CommitsLoaded => ViewModel.Loaded;

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChooseCommitViewModel.SelectedIndex) && ViewModel.SelectedIndex >= 0)
        {
            revisionGrid.ScrollIntoView(ViewModel.SelectedIndex);
        }
    }
}
