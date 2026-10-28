using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using GitCommands;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormViewPatch</c> (reading in <see cref="ViewPatchDialog"/>): a patch file's file patches, sortable by
///  file name, change and type as upstream's grid columns, and the selected one's diff.
/// </summary>
public partial class ViewPatchWindow : Window
{
    private readonly GitModule _module;
    private IReadOnlyList<Patch> _patches = [];
    private int? _sortColumn;
    private bool _descending;

    public ViewPatchWindow()
        : this("", null)
    {
    }

    /// <param name="repositoryPath">The repository whose files' encoding reads the patch.</param>
    /// <param name="patchFile">The patch file shown at first (upstream's <c>LoadPatch</c>), or null.</param>
    public ViewPatchWindow(string repositoryPath, string? patchFile)
    {
        _module = GitModules.Open(repositoryPath);
        InitializeComponent();
        UpstreamTranslation.Apply(this, "FormViewPatch");
        InitialFocus.Attach(this);
        GridChangedFiles.ItemTemplate = new FuncDataTemplate<Patch>((patch, _) => PatchRow(patch));
        GridChangedFiles.SelectionChanged += (_, _) => ShowSelectedPatch();
        FileNameA.Click += (_, _) => SortBy(0);
        typeDataGridViewTextBoxColumn.Click += (_, _) => SortBy(1);
        File.Click += (_, _) => SortBy(2);
        BrowsePatch.Click += (_, _) => UiActions.Run(BrowseAsync, ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));
        if (patchFile is not null)
        {
            PatchFileNameEdit.Text = patchFile;
            LoadPatchFile();
        }
    }

    /// <summary>
    ///  The file patches as listed.
    /// </summary>
    public IReadOnlyList<Patch> Patches => GridChangedFiles.ItemsSource as IReadOnlyList<Patch> ?? [];

    // Upstream's BrowsePatch_Click: a .patch file, then the list (read again even when cancelled, as upstream's).
    private async Task BrowseAsync()
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = UpstreamTranslation.ByText(ViewPatchDialog.PatchFileFilterTitle),
            FileTypeFilter = [new FilePickerFileType(UpstreamTranslation.ByText(ViewPatchDialog.PatchFileFilter))
            {
                Patterns = ["*.patch"],
            }],
        });
        if (files.FirstOrDefault()?.TryGetLocalPath() is { } path)
        {
            PatchFileNameEdit.Text = path;
        }

        LoadPatchFile();
    }

    // Upstream's LoadPatchFile.
    private void LoadPatchFile()
    {
        _patches = ViewPatchDialog.Read(PatchFileNameEdit.Text ?? "", () => _module.FilesEncoding);
        ShowPatches();
    }

    // Upstream's SortablePatchesList: a header click sorts by its column, a second click the other way.
    private void SortBy(int column)
    {
        _descending = _sortColumn == column && !_descending;
        _sortColumn = column;
        ShowPatches();
    }

    private void ShowPatches()
    {
        IEnumerable<Patch> patches = _patches;
        if (_sortColumn is { } column)
        {
            Func<Patch, string> key = patch => Columns(patch)[column];
            patches = _descending
                ? patches.OrderByDescending(key, StringComparer.Ordinal)
                : patches.OrderBy(key, StringComparer.Ordinal);
        }

        GridChangedFiles.ItemsSource = patches.ToList();
    }

    // Upstream's GridChangedFiles_SelectionChanged: ViewFixedPatch of the selected file patch.
    private void ShowSelectedPatch()
    {
        if (GridChangedFiles.SelectedItem is Patch patch)
        {
            ChangesList.ShowPatch(patch.FileNameB ?? "", patch.Text ?? "");
        }
    }

    // Upstream's columns: FileNameA, ChangeType and FileType.
    private static string[] Columns(Patch patch)
        => [patch.FileNameA ?? "", patch.ChangeType.ToString(), patch.FileType.ToString()];

    private static Control PatchRow(Patch? patch)
    {
        Grid row = new() { ColumnDefinitions = new ColumnDefinitions("*,70,50") };
        if (patch is null)
        {
            return row;
        }

        string[] texts = Columns(patch);
        for (int column = 0; column < texts.Length; column++)
        {
            TextBlock text = new()
            {
                Text = texts[column],
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(text, column);
            row.Children.Add(text);
        }

        return row;
    }
}
