using System.Collections.ObjectModel;
using Avalonia.Controls;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Ui;
using Button = Avalonia.Controls.Button;

namespace GitExtensions.Plugins.FindLargeFiles;

/// <summary>
///  The new shell's version of upstream's <c>FindLargeFilesForm</c>: lists the files in HEAD's history at least as big as the
///  plugin's threshold while <see cref="LargeFileFinder"/> reads it, and removes the chosen ones from the whole history with
///  upstream's script (<see cref="FindLargeFilesScript"/>).
/// </summary>
public partial class FindLargeFilesWindow : Window
{
    private const string Category = "FindLargeFilesForm";
    private const string AreYouSureToDelete = "Are you sure to delete the selected files?";
    private const string DeleteCaption = "Delete";

    private readonly IGitUICommands _commands;
    private readonly LargeFileFinder _finder;
    private readonly ObservableCollection<LargeFileRow> _rows = [];
    private readonly Dictionary<GitObject, LargeFileRow> _rowOf = [];
    private readonly CancellationTokenSource _closed = new();
    private Func<LargeFileRow, IComparable>? _sortKey;
    private bool _sortDescending;

    public FindLargeFilesWindow(float threshold, IGitUICommands commands)
    {
        _commands = commands;
        _finder = new LargeFileFinder(commands.Module, threshold);
        InitializeComponent();
        UpstreamTranslation.Apply(this, Category);
        FileList.ItemsSource = _rows;
        Cancel.Click += (_, _) => Close();
        Delete.Click += (_, _) => DeleteSelected();
        SortBy(sHADataGridViewTextBoxColumn, row => row.Sha);
        SortBy(pathDataGridViewTextBoxColumn, row => row.Path);
        SortBy(sizeDataGridViewTextBoxColumn, row => row.File.SizeInBytes);
        SortBy(CompressedSize, row => row.File.CompressedSizeInBytes);
        SortBy(commitCountDataGridViewTextBoxColumn, row => row.CommitCount);
        SortBy(lastCommitDateDataGridViewTextBoxColumn, row => row.File.LastCommitDate);
        Opened += (_, _) => _ = ScanAsync();
        Closed += (_, _) => _closed.Cancel();
    }

    /// <summary>
    ///  True once the history has been read; the rows can be ticked from then on, as upstream's grid becomes editable.
    /// </summary>
    public bool IsScanDone { get; private set; }

    private async Task ScanAsync()
    {
        _finder.ReadRevisions();
        ScanProgress.Maximum = _finder.ProgressMaximum;
        Progress<GitObject> added = new(file =>
        {
            LargeFileRow row = new(file);
            _rowOf[file] = row;
            _rows.Add(row);
        });
        Progress<GitObject> changed = new(file => _rowOf.GetValueOrDefault(file)?.Refresh());
        Progress<int> progress = new(value => ScanProgress.Value = value);
        try
        {
            CancellationToken token = _closed.Token;
            await Task.Run(() => _finder.Scan(added, changed, progress, token), token);
        }
        catch (Exception ex) when (ex is OperationCanceledException or ExternalOperationException)
        {
            // Upstream ignores every failure of the scan; the list keeps what was found.
        }

        // Progress<T> posts its reports to this thread; they run before the scan is shown as done.
        await Task.Yield();
        ScanProgress.IsVisible = false;
        foreach (LargeFileRow row in _rows)
        {
            row.IsEditable = true;
        }

        IsScanDone = true;
    }

    private void SortBy(Button header, Func<LargeFileRow, IComparable> key)
    {
        header.Click += (_, _) =>
        {
            _sortDescending = _sortKey == key && !_sortDescending;
            _sortKey = key;
            List<LargeFileRow> sorted = _sortDescending
                ? [.. _rows.OrderByDescending(key)]
                : [.. _rows.OrderBy(key)];
            _rows.Clear();
            foreach (LargeFileRow row in sorted)
            {
                _rows.Add(row);
            }
        };
    }

    // Upstream's Delete_Click: the window closes whatever the answer.
    private void DeleteSelected()
    {
        if (MessageBoxes.Show(new WindowOwner(this),
                UpstreamTranslation.Text(Category, "_areYouSureToDelete", AreYouSureToDelete),
                UpstreamTranslation.Text(Category, "_deleteCaption", DeleteCaption),
                System.Windows.Forms.MessageBoxButtons.YesNo, System.Windows.Forms.MessageBoxIcon.Warning)
            == System.Windows.Forms.DialogResult.Yes)
        {
            IEnumerable<string> paths = _rows.Where(row => row.Delete).Select(row => row.Path);
            _commands.StartBatchFileProcessDialog(FindLargeFilesScript.Generate(paths, OperatingSystem.IsWindows()));
        }

        Close();
    }
}
