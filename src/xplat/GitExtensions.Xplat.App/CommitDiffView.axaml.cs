using Avalonia.Controls;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>CommitDiff</c>: a commit's information, its changed files and the selected file's diff, bound to
///  <see cref="CommitDiffViewModel"/>. Shared by the commit diff window (upstream's <c>FormCommitDiff</c>) and the file
///  history's Commit tab. The commit information control is not bound yet, so the view hands it what to show.
/// </summary>
public partial class CommitDiffView : UserControl
{
    public CommitDiffView()
    {
        IAppPreferences preferences = AppServices.Preferences;
        ViewModel = new CommitDiffViewModel(
            new GitCommitHistory(() => new CommitDateStyle(preferences.RelativeDate, preferences.ShowAuthorDate),
                links: AppServices.RevisionLinks),
            DiffView.NewViewModel());
        InitializeComponent();
        LayoutRoot.DataContext = ViewModel;
        ViewModel.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(CommitDiffViewModel.Info):
                    CommitInfo.RepositoryPath = ViewModel.RepositoryPath;
                    CommitInfo.Show(ViewModel.Info.Details, ViewModel.Info.Error, []);
                    break;
                case nameof(CommitDiffViewModel.Title):
                    TitleChanged?.Invoke(this, EventArgs.Empty);
                    break;
                case nameof(CommitDiffViewModel.SelectedFile) when ViewModel.SelectedFile is { } file:
                    DiffFiles.ScrollIntoView(file);
                    break;
            }
        };

        // Upstream CommitDiff's FileViewer_TopScrollReached and FileViewer_BottomScrollReached.
        DiffText.TopScrollReached += (_, _) =>
        {
            ViewModel.SelectNeighbourFile(next: false);
            DiffText.ScrollToBottom();
        };
        DiffText.BottomScrollReached += (_, _) =>
        {
            ViewModel.SelectNeighbourFile(next: true);
            DiffText.ScrollToTop();
        };
    }

    public CommitDiffViewModel ViewModel { get; }

    /// <summary>
    ///  Raised when <see cref="Title"/> changes, as upstream's <c>TextChanged</c>.
    /// </summary>
    public event EventHandler? TitleChanged;

    /// <inheritdoc cref="CommitDiffViewModel.Title"/>
    public string Title => ViewModel.Title;

    /// <summary>
    ///  The commit information, whose links the host may handle.
    /// </summary>
    public CommitInfoView Info => CommitInfo;

    /// <inheritdoc cref="CommitDiffViewModel.SetRevisionAsync"/>
    public Task SetRevisionAsync(string repositoryPath, string hash, string? fileToSelect)
        => ViewModel.SetRevisionAsync(repositoryPath, hash, fileToSelect);
}
