using System.Text.RegularExpressions;
using Avalonia.Controls;
using GitCommands;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FileStatusList</c> for the files of a commit or of a diff between two revisions (the browse window's Diff
///  tab, FormDiff): the group-by buttons (upstream's <c>DiffListSortType</c>, shared with the commit window's lists), the
///  regular expression filter (an invalid one shows its error as a tooltip and filters nothing) and the NoFiles text while
///  a loaded list shows no file. The selected files stay selected when the list is grouped or filtered again.
/// </summary>
internal sealed class RevisionFileList<T>
    where T : class
{
    // Upstream's texts.
    public const string NoChanges = "No changes";
    private const string RegexErrorTitle = "Regex error";

    private readonly ListBox _list;
    private readonly TextBox _filter;
    private readonly ToolToggleButton _byPath;
    private readonly ToolToggleButton _byExtension;
    private readonly ToolToggleButton _byStatus;
    private readonly TextBlock _noFiles;
    private readonly IAppPreferences _preferences;
    private readonly Func<T, string> _path;
    private readonly Func<T, string> _status;
    private IReadOnlyList<T>? _files;

    public RevisionFileList(ListBox list, TextBox filter, ToolToggleButton byPath, ToolToggleButton byExtension,
        ToolToggleButton byStatus, TextBlock noFiles, IAppPreferences preferences, Func<T, string> path,
        Func<T, string> status)
    {
        _list = list;
        _filter = filter;
        _byPath = byPath;
        _byExtension = byExtension;
        _byStatus = byStatus;
        _noFiles = noFiles;
        _preferences = preferences;
        _path = path;
        _status = status;
        _noFiles.Text = UpstreamTranslation.ByText(NoChanges);
        _noFiles.IsVisible = false;
        _filter.TextChanged += (_, _) => Show();
        _byPath.Click += (_, _) => SetGroupBy(DiffListSortType.FilePath);
        _byExtension.Click += (_, _) => SetGroupBy(DiffListSortType.FileExtension);
        _byStatus.Click += (_, _) => SetGroupBy(DiffListSortType.FileStatus);
        ShowGroupBy();
    }

    /// <summary>
    ///  The files to list; null while they are being read (the list is empty, without the NoFiles text).
    /// </summary>
    public IReadOnlyList<T>? Files
    {
        get => _files;
        set
        {
            _files = value;
            Show();
        }
    }

    // Upstream's filtered and grouped list, keeping the selection.
    private void Show()
    {
        HashSet<string> selected = [.. (_list.SelectedItems ?? Array.Empty<object>()).OfType<T>().Select(_path)];
        Regex? filter = FileStatusList.Filter(_filter.Text, out string? error);
        ToolTip.SetTip(_filter, error is null ? null : $"{RegexErrorTitle}: {error}");
        if (_files is null)
        {
            _list.ItemsSource = null;
            _noFiles.IsVisible = false;
            return;
        }

        IReadOnlyList<T> shown = FileStatusList.Sort(_files.Where(file => filter?.IsMatch(_path(file)) ?? true),
            _preferences.DiffListSorting, _path, file => FileStatusList.KindOf(_status(file)));
        _list.ItemsSource = shown;
        _noFiles.IsVisible = shown.Count == 0;
        foreach (T file in shown.Where(file => selected.Contains(_path(file))))
        {
            _list.SelectedItems?.Add(file);
        }
    }

    // Upstream's group-by buttons choose the grouping and keep the tree or flat variant.
    private void SetGroupBy(DiffListSortType sorting)
    {
        bool flat = _preferences.DiffListSorting.ToString().EndsWith("Flat", StringComparison.Ordinal);
        _preferences.DiffListSorting = flat ? Enum.Parse<DiffListSortType>($"{sorting}Flat") : sorting;
        _preferences.Save();
        ShowGroupBy();
        Show();
    }

    private void ShowGroupBy()
    {
        DiffListSortType sorting = _preferences.DiffListSorting;
        _byPath.IsChecked = sorting is DiffListSortType.FilePath or DiffListSortType.FilePathFlat;
        _byExtension.IsChecked = sorting is DiffListSortType.FileExtension or DiffListSortType.FileExtensionFlat;
        _byStatus.IsChecked = sorting is DiffListSortType.FileStatus or DiffListSortType.FileStatusFlat;
    }
}
