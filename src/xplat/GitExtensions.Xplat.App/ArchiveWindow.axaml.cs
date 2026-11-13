using Avalonia.Controls;
using Avalonia.Platform.Storage;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormArchive</c>, bound to <see cref="ArchiveViewModel"/> (rules in <see cref="ArchiveDialog"/>): archives a
///  revision as zip or tar, all of it, only some paths, or only the files changed since a second revision.
/// </summary>
public partial class ArchiveWindow : Window
{
    /// <summary>
    ///  The design-time constructor (<see cref="DesignTime"/>): the window's XAML only.
    /// </summary>
    [Obsolete(DesignTime.ConstructorOnly)]
#pragma warning disable CS8618 // The fields the window's data fills stay unset: no code of the window runs.
    public ArchiveWindow() => InitializeComponent();
#pragma warning restore CS8618

    /// <param name="repositoryPath">The repository.</param>
    /// <param name="revision">The revision to archive.</param>
    /// <param name="diffRevision">The revision to compare with (upstream's second selected revision), or null.</param>
    /// <param name="path">The path to archive alone (upstream's <c>SetPathArgument</c>), or null.</param>
    /// <param name="preferences">The commit chooser's grid settings.</param>
    /// <param name="archive">Runs git with the archive arguments, showing its output (upstream's FormProcess).</param>
    public ArchiveWindow(string repositoryPath, string revision, string? diffRevision, string? path,
        IAppPreferences preferences, Func<string, Task>? archive)
    {
        ViewModel = new ArchiveViewModel(new WindowDialogHost(this), GitModules.Open(repositoryPath), revision, diffRevision,
            path, archive,
            async current => (await new ChooseCommitWindow(repositoryPath, current, preferences)
                .ShowDialog<CommitRow?>(this))?.Hash,
            PickWithDialogAsync);
        DataContext = ViewModel;
        InitializeComponent();
        UpstreamTranslation.Apply(this, ArchiveViewModel.Category);
        InitialFocus.Attach(this, () => buttonArchiveRevision);
        Opened += (_, _) => UiActions.Run(ViewModel.LoadAsync, ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));
    }

    public ArchiveViewModel ViewModel { get; }

    /// <summary>
    ///  Asks for the archive's file: the suggested name and the extension; null when cancelled. The save picker by default;
    ///  tests replace it.
    /// </summary>
    public Func<string, string, Task<string?>> PickOutput
    {
        get => ViewModel.PickOutput;
        set => ViewModel.PickOutput = value;
    }

    /// <summary>
    ///  Completes when the revisions are shown, for tests.
    /// </summary>
    public Task RevisionsLoaded => ViewModel.Loaded;

    // Upstream's SaveFileDialog: its title, the format's filter and the suggested name.
    private async Task<string?> PickWithDialogAsync(string suggestion, string extension)
    {
        string caption = UpstreamTranslation.ByText(extension == "zip"
            ? ArchiveDialog.SaveFileDialogFilterZip
            : ArchiveDialog.SaveFileDialogFilterTar);
        IStorageFile? file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = UpstreamTranslation.ByText(ArchiveDialog.SaveFileDialogCaption),
            SuggestedFileName = suggestion,
            DefaultExtension = extension,
            FileTypeChoices = [new FilePickerFileType(caption) { Patterns = [$"*.{extension}"] }],
        });
        return file?.TryGetLocalPath();
    }
}
