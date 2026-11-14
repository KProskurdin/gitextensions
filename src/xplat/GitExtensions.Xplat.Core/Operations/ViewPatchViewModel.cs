using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Presentation;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  The view model of upstream's <c>FormViewPatch</c> (reading in <see cref="ViewPatchDialog"/>): a patch file's file
///  patches, sortable by file name, change and type as upstream's grid columns. The window shows the selected one's diff.
/// </summary>
public sealed partial class ViewPatchViewModel : DialogViewModel
{
    /// <summary>
    ///  The upstream form whose texts the window shows, by their names.
    /// </summary>
    public const string Category = "FormViewPatch";

    private readonly IGitModule _module;
    private IReadOnlyList<Patch> _read = [];
    private int? _sortColumn;
    private bool _descending;

    /// <param name="module">The repository whose files' encoding reads the patch.</param>
    /// <param name="patchFile">The patch file shown at first (upstream's <c>LoadPatch</c>), or null.</param>
    public ViewPatchViewModel(IDialogHost host, IGitModule module, string? patchFile)
        : base(host)
    {
        _module = module;
        if (patchFile is not null)
        {
            PatchFile = patchFile;
            LoadPatchFile();
        }
    }

    [ObservableProperty]
    public partial string? PatchFile { get; set; }

    /// <summary>
    ///  The file patches as listed.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<Patch> Patches { get; private set; } = [];

    /// <summary>
    ///  The file patch whose diff shows.
    /// </summary>
    [ObservableProperty]
    public partial Patch? SelectedPatch { get; set; }

    /// <summary>
    ///  Picks a .patch file; null when cancelled. The window sets it, as it is the platform's picker.
    /// </summary>
    public Func<Task<string?>>? PickPatchFile { get; set; }

    /// <summary>
    ///  Upstream's columns of a file patch: its file name, change and type.
    /// </summary>
    public static string[] Columns(Patch patch)
        => [patch.FileNameA ?? "", patch.ChangeType.ToString(), patch.FileType.ToString()];

    // Upstream's BrowsePatch_Click: a .patch file, then the list (read again even when cancelled, as upstream's).
    [RelayCommand]
    private Task BrowseAsync() => GuardAsync(async () =>
    {
        if (PickPatchFile is not null && await PickPatchFile() is { } path)
        {
            PatchFile = path;
        }

        LoadPatchFile();
    });

    // Upstream's SortablePatchesList: a header click sorts by its column, a second click the other way.
    [RelayCommand]
    private void SortBy(int column)
    {
        _descending = _sortColumn == column && !_descending;
        _sortColumn = column;
        ShowPatches();
    }

    // Upstream's LoadPatchFile.
    private void LoadPatchFile()
    {
        _read = ViewPatchDialog.Read(PatchFile ?? "", () => _module.FilesEncoding);
        ShowPatches();
    }

    private void ShowPatches()
    {
        IEnumerable<Patch> patches = _read;
        if (_sortColumn is { } column)
        {
            Func<Patch, string> key = patch => Columns(patch)[column];
            patches = _descending
                ? patches.OrderByDescending(key, StringComparer.Ordinal)
                : patches.OrderBy(key, StringComparer.Ordinal);
        }

        Patches = [.. patches];
    }
}
