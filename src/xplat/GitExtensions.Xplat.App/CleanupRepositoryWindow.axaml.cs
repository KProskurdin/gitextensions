using Avalonia.Controls;
using Avalonia.Platform.Storage;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormCleanupRepository</c>, bound to <see cref="CleanupRepositoryViewModel"/> (rules in
///  <see cref="CleanupDialog"/>): <c>git clean</c> with upstream's choices, previewed with a dry run, run after upstream's
///  question; git's output goes to the log. Upstream shows each run in its process window too; here the log is the only
///  place.
/// </summary>
public partial class CleanupRepositoryWindow : Window
{
    public CleanupRepositoryWindow()
        : this("", null)
    {
    }

    /// <param name="repositoryPath">The repository to clean.</param>
    /// <param name="cleaned">Called after a cleanup ran, so the browse window reads the work tree again.</param>
    public CleanupRepositoryWindow(string repositoryPath, Action? cleaned)
    {
        ViewModel = new CleanupRepositoryViewModel(new WindowDialogHost(this), GitModules.Open(repositoryPath), cleaned)
        {
            PickFolder = PickFolderAsync,
            PickFile = PickFileAsync,
        };
        DataContext = ViewModel;
        InitializeComponent();
        UpstreamTranslation.Apply(this, CleanupRepositoryViewModel.Category);
        InitialFocus.Attach(this);
    }

    public CleanupRepositoryViewModel ViewModel { get; }

    /// <summary>
    ///  The last run of git, for tests.
    /// </summary>
    public Task LastRun => ViewModel.LastRun;

    private async Task<string?> PickFolderAsync(string startPath)
    {
        IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(startPath),
        });
        return folders.FirstOrDefault()?.TryGetLocalPath();
    }

    private async Task<string?> PickFileAsync(string startPath)
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(startPath),
        });
        return files.FirstOrDefault()?.TryGetLocalPath();
    }
}
