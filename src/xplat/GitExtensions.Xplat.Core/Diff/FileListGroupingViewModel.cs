using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitCommands;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.Core.Diff;

/// <summary>
///  Upstream <c>FileStatusList</c>'s grouping of its files, one setting for every list: the group-by buttons, the tree split
///  button (flat list or tree) and its menu's six groupings, "Dense tree" and "Show group nodes in flat list". Each command
///  stores upstream's setting and raises <see cref="Changed"/>, on which the view shows its lists again.
/// </summary>
public sealed partial class FileListGroupingViewModel(IAppPreferences preferences) : ObservableObject
{
    // Upstream's DiffListSortType names its flat variants with this suffix.
    private const string FlatSuffix = "Flat";

    /// <summary>
    ///  Raised after a command changed the grouping.
    /// </summary>
    public event EventHandler? Changed;

    public DiffListSortType Sorting => preferences.DiffListSorting;

    public bool IsByPath => Sorting is DiffListSortType.FilePath or DiffListSortType.FilePathFlat;

    public bool IsByExtension => Sorting is DiffListSortType.FileExtension or DiffListSortType.FileExtensionFlat;

    public bool IsByStatus => Sorting is DiffListSortType.FileStatus or DiffListSortType.FileStatusFlat;

    /// <summary>
    ///  Upstream's tree button image: the flat list's while a flat variant is chosen.
    /// </summary>
    public string TreeImage => IsFlat(Sorting) ? "DocumentTree" : "FileTree";

    public bool DenseTree => preferences.FileStatusMergeSingleItemWithFolder;

    /// <summary>
    ///  Upstream's <c>UpdateToolbar</c>: a dense tree only for a tree.
    /// </summary>
    public bool CanDenseTree => !IsFlat(Sorting);

    public bool ShowGroupNodesInFlatList => preferences.FileStatusShowGroupNodesInFlatList;

    /// <summary>
    ///  Upstream's <c>UpdateToolbar</c>: group nodes only for a flat list grouped by extension or status.
    /// </summary>
    public bool CanShowGroupNodesInFlatList => IsFlat(Sorting) && Sorting is not DiffListSortType.FilePathFlat;

    /// <summary>
    ///  Upstream's group-by buttons: the grouping chosen keeps the tree or flat variant.
    /// </summary>
    public static DiffListSortType GroupedBy(DiffListSortType current, DiffListSortType grouping)
        => IsFlat(current) ? Enum.Parse<DiffListSortType>(grouping + FlatSuffix) : grouping;

    /// <summary>
    ///  Shows the stored grouping again, which another window may have changed.
    /// </summary>
    public void Refresh() => OnPropertyChanged(string.Empty);

    [RelayCommand]
    private void GroupBy(DiffListSortType grouping) => Set(GroupedBy(Sorting, grouping));

    [RelayCommand]
    private void SetSorting(DiffListSortType sorting) => Set(sorting);

    // Upstream's AsTree_ButtonClick: the flat variant of the grouping, or its tree.
    [RelayCommand]
    private void ToggleTree()
    {
        string sorting = Sorting.ToString();
        Set(Enum.Parse<DiffListSortType>(sorting.EndsWith(FlatSuffix, StringComparison.Ordinal)
            ? sorting[..^FlatSuffix.Length]
            : sorting + FlatSuffix));
    }

    [RelayCommand]
    private void ToggleDenseTree()
    {
        preferences.FileStatusMergeSingleItemWithFolder = !preferences.FileStatusMergeSingleItemWithFolder;
        Set(Sorting);
    }

    [RelayCommand]
    private void ToggleShowGroupNodesInFlatList()
    {
        preferences.FileStatusShowGroupNodesInFlatList = !preferences.FileStatusShowGroupNodesInFlatList;
        Set(Sorting);
    }

    // Every property is raised, also those left as they were: a toggle button clicked while checked unchecks itself, and the
    // binding checks it again.
    private void Set(DiffListSortType sorting)
    {
        preferences.DiffListSorting = sorting;
        preferences.Save();
        Refresh();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static bool IsFlat(DiffListSortType sorting)
        => sorting.ToString().EndsWith(FlatSuffix, StringComparison.Ordinal);
}
