using Avalonia.Controls;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Xplat.Core.CommitHistory;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Lists the files of a commit and the history of the file selected in it, bound to <see cref="FileBrowserViewModel"/>.
/// </summary>
public partial class FileBrowserWindow : Window
{
    /// <summary>
    ///  The design-time constructor (<see cref="DesignTime"/>): the window's XAML only.
    /// </summary>
    [Obsolete(DesignTime.ConstructorOnly)]
#pragma warning disable CS8618 // The fields the window's data fills stay unset: no code of the window runs.
    public FileBrowserWindow() => InitializeComponent();
#pragma warning restore CS8618

    /// <param name="repositoryHost">The repository host plugin of the repository, for the blame menu (e.g. "View in GitHub").</param>
    /// <param name="selectedFile">A file to select, whose history then shows (upstream's file history of a file).</param>
    public FileBrowserWindow(string repositoryPath, string hash, Func<IRepositoryHostPlugin?>? repositoryHost = null,
        string? selectedFile = null)
    {
        ViewModel = new FileBrowserViewModel(
            new GitCommitHistory(
                () => new CommitDateStyle(AppServices.Preferences.RelativeDate, AppServices.Preferences.ShowAuthorDate),
                () => AppServices.Preferences.RevisionSortOrder,
                fileHistory: () => AppServices.Preferences.FileHistory),
            AppServices.Preferences)
        {
            // The windows the commands open.
            OpenBlame = file => new BlameWindow(repositoryPath, hash, file, repositoryHost).Show(this),
            OpenDiff = (commit, file) => new DiffWindow(repositoryPath, commit, file, staged: false).Show(this),
        };
        DataContext = ViewModel;
        InitializeComponent();
        WindowPlacementTracker.Attach(this, "Xplat.FileBrowserWindow");
        Opened += (_, _) => _ = ViewModel.OpenAsync(repositoryPath, hash, selectedFile);
    }

    public FileBrowserViewModel ViewModel { get; }
}
