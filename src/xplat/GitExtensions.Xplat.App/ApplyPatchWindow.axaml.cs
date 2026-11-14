using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;
using GitExtUtils.GitUI.Theming;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormApplyPatch</c>, bound to <see cref="ApplyPatchViewModel"/> (rules in <see cref="ApplyPatchDialog"/>):
///  a patch file (<c>git apply</c> for a raw diff, <c>git am</c> for a mail) or a directory of patches applied, and while
///  <c>git am</c> stopped, its patches with the one being applied, Conflicts resolved, Skip, Abort, Add files and Solve
///  conflicts. Git runs through the caller with its output shown, as upstream's FormProcess.
/// </summary>
public partial class ApplyPatchWindow : Window
{
    public ApplyPatchWindow()
        : this("", AppServices.Preferences, null, null, null)
    {
    }

    /// <param name="repositoryPath">The repository.</param>
    /// <param name="preferences">Upstream's two apply patch options.</param>
    /// <param name="runGit">Runs git with the arguments, showing its output; true when it succeeded.</param>
    /// <param name="showConflicts">Upstream's StartResolveConflictsDialog.</param>
    /// <param name="changed">Tells the browse window the repository changed (upstream's RepoChangedNotifier).</param>
    public ApplyPatchWindow(string repositoryPath, IAppPreferences preferences, Func<string, Task<bool>>? runGit,
        Action? showConflicts, Action? changed)
    {
        ViewModel = new ApplyPatchViewModel(new WindowDialogHost(this), GitModules.Open(repositoryPath), preferences,
            runGit, showConflicts, changed)
        {
            PickPatchFile = PickPatchFileAsync,
            PickPatchDir = PickPatchDirAsync,
            ShowAddFiles = () => new AddFilesWindow(null, runGit).ShowDialog(this),
        };
        InitializeComponent();
        UpstreamTranslation.Apply(this, ApplyPatchViewModel.Category);

        // After the translation pass, which would leave the title without the working directory.
        DataContext = ViewModel;
        InitialFocus.Attach(this, () => PatchFile);

        // View concerns: the highlighted default button, the patches in their colors, a double click opening a patch.
        ShowDefault(ViewModel.Buttons.Default);
        ViewModel.PropertyChanged += OnViewModelChanged;

        // Upstream calls EnableButtons when the conflicts window returns; this window is told by being activated again.
        Activated += (_, _) => ViewModel.Refresh();
        PatchGrid.DoubleTapped += (_, _) => UiActions.Run(
            () => RebasePatchRow.OpenAsync(this, repositoryPath, PatchGrid.SelectedItem as RebasePatchRow),
            ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));
    }

    public ApplyPatchViewModel ViewModel { get; }

    /// <summary>
    ///  The last refresh of the buttons and patches, for tests.
    /// </summary>
    public Task Refreshed => ViewModel.Refreshed;

    /// <summary>
    ///  Upstream's <c>SetPatchFile</c>.
    /// </summary>
    public void SetPatchFile(string name) => ViewModel.SetPatchFile(name);

    /// <summary>
    ///  Upstream's <c>SetPatchDir</c>.
    /// </summary>
    public void SetPatchDir(string name) => ViewModel.SetPatchDir(name);

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ApplyPatchViewModel.Buttons))
        {
            ShowDefault(ViewModel.Buttons.Default);
        }
        else if (e.PropertyName == nameof(ApplyPatchViewModel.Patches))
        {
            ShowPatches(ViewModel.Patches);
        }
    }

    // Upstream's highlight of the default button: its text between > and <, and a frame.
    private void ShowDefault(ApplyPatchDefault highlighted)
    {
        string resolved = UpstreamTranslation.ByText(ApplyPatchDialog.ConflictResolved);
        string mergetool = UpstreamTranslation.ByText(ApplyPatchDialog.ConflictMergetool);
        ResolvedText.Text = UpstreamTranslation.WithAccessKey(highlighted == ApplyPatchDefault.Resolved
            ? ApplyPatchDialog.Highlighted(resolved)
            : resolved);
        MergetoolText.Text = UpstreamTranslation.WithAccessKey(highlighted == ApplyPatchDefault.Mergetool
            ? ApplyPatchDialog.Highlighted(mergetool)
            : mergetool);
        Resolved.IsDefault = highlighted == ApplyPatchDefault.Resolved;
        Mergetool.IsDefault = highlighted == ApplyPatchDefault.Mergetool;
        IBrush frame = (IBrush?)this.FindResource("SystemControlForegroundBaseHighBrush") ?? Brushes.Black;
        ContinuePanel.Background = highlighted == ApplyPatchDefault.Resolved ? frame : Brushes.Transparent;
        MergeToolPanel.Background = highlighted == ApplyPatchDefault.Mergetool ? frame : Brushes.Transparent;
    }

    // The PatchGrid's DisplayPatches: the patch being applied selected, in OrangeRed.
    private void ShowPatches(IReadOnlyList<RebasePatch> patches)
    {
        System.Drawing.Color red = System.Drawing.Color.OrangeRed.AdaptForeColor(ThemeBrushes.Current.IsDark
            ? System.Drawing.Color.FromArgb(0x20, 0x20, 0x20)
            : System.Drawing.Color.White);
        IBrush applying = new SolidColorBrush(Color.FromArgb(red.A, red.R, red.G, red.B));
        List<RebasePatchRow> rows = [.. patches.Select(patch => new RebasePatchRow(patch, patch.IsNext ? applying : null))];
        PatchGrid.ItemsSource = rows;
        PatchGrid.SelectedItem = rows.FirstOrDefault(row => row.Patch.IsNext);
    }

    // Upstream's BrowsePatch_Click: a .patch file.
    private async Task<string?> PickPatchFileAsync()
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = UpstreamTranslation.ByText(ApplyPatchDialog.SelectPatchFileCaption),
            FileTypeFilter =
            [
                new FilePickerFileType(UpstreamTranslation.ByText(ApplyPatchDialog.SelectPatchFileFilter))
                {
                    Patterns = ["*.patch"],
                },
            ],
        });
        return files.FirstOrDefault()?.TryGetLocalPath();
    }

    // Upstream's BrowseDir_Click.
    private async Task<string?> PickPatchDirAsync()
    {
        IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions());
        return folders.FirstOrDefault()?.TryGetLocalPath();
    }
}
