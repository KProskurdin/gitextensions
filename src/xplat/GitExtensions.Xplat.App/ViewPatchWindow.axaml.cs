using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormViewPatch</c>, bound to <see cref="ViewPatchViewModel"/> (reading in <see cref="ViewPatchDialog"/>):
///  a patch file's file patches, sortable by file name, change and type as upstream's grid columns, and the selected one's
///  diff.
/// </summary>
public partial class ViewPatchWindow : Window
{
    public ViewPatchWindow()
        : this("", null)
    {
    }

    /// <param name="repositoryPath">The repository whose files' encoding reads the patch.</param>
    /// <param name="patchFile">The patch file shown at first (upstream's <c>LoadPatch</c>), or null.</param>
    public ViewPatchWindow(string repositoryPath, string? patchFile)
    {
        ViewModel = new ViewPatchViewModel(new WindowDialogHost(this), GitModules.Open(repositoryPath), patchFile)
        {
            PickPatchFile = PickPatchFileAsync,
        };
        DataContext = ViewModel;
        InitializeComponent();
        UpstreamTranslation.Apply(this, ViewPatchViewModel.Category);
        InitialFocus.Attach(this);

        // View concern: the diff control shows the selected file patch (upstream's ViewFixedPatch).
        ViewModel.PropertyChanged += OnViewModelChanged;
    }

    public ViewPatchViewModel ViewModel { get; }

    /// <summary>
    ///  The file patches as listed.
    /// </summary>
    public IReadOnlyList<Patch> Patches => ViewModel.Patches;

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewPatchViewModel.SelectedPatch) && ViewModel.SelectedPatch is { } patch)
        {
            ChangesList.ShowPatch(patch.FileNameB ?? "", patch.Text ?? "");
        }
    }

    private async Task<string?> PickPatchFileAsync()
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = UpstreamTranslation.ByText(ViewPatchDialog.PatchFileFilterTitle),
            FileTypeFilter =
            [
                new FilePickerFileType(UpstreamTranslation.ByText(ViewPatchDialog.PatchFileFilter))
                {
                    Patterns = ["*.patch"],
                },
            ],
        });
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }
}
