using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using GitCommands;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;
using DialogResult = System.Windows.Forms.DialogResult;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormVerify</c> (Repository > Git maintenance > Recover lost objects...; rules in
///  <see cref="VerifyDatabase"/>): the objects <c>git fsck-objects</c> finds with the chosen options, their content, and
///  upstream's ways to recover them: LOST_FOUND_ tags on the checked ones, a tag or branch on one, saving a blob, or
///  saving them all to .git/lost-found.
/// </summary>
public partial class VerifyDatabaseWindow : Window
{
    // Upstream's column order: the check box, then Date, Type, Subject, Author, Hash and Parent.
    private const int DateColumn = 1;
    private const int TypeColumn = 2;
    private const int SubjectColumn = 3;
    private const int AuthorColumn = 4;
    private const int HashColumn = 5;
    private const int ParentColumn = 6;
    private const double HashColumnWidth = 60;
    private const double AuthorColumnWidth = 150;

    private readonly GitModule _module;
    private readonly Func<string, Task<bool>> _runGitWithOutput;
    private readonly Func<Window, ObjectId, Task<bool>> _createTag;
    private readonly Func<Window, ObjectId, Task<bool>> _createBranch;
    private IReadOnlyList<LostObjectRow> _rows = [];
    private int _sortColumn = DateColumn;
    private bool _descending = true;
    private bool _typeDetected;
    private LostObjectRow? _previewed;
    private string? _defaultFileName;
    private Task _objectsLoaded = Task.CompletedTask;
    private Task _previewShown = Task.CompletedTask;

    public VerifyDatabaseWindow()
        : this("", _ => Task.FromResult(false), (_, _) => Task.FromResult(false), (_, _) => Task.FromResult(false))
    {
    }

    /// <param name="repositoryPath">The repository whose objects are checked.</param>
    /// <param name="runGitWithOutput">
    ///  Runs a git command with its output shown (upstream's FormProcess), for "Save objects to .git/lost-found" and
    ///  "Remove all dangling objects".
    /// </param>
    /// <param name="createTag">Opens the create tag window on an object, true when a tag was created.</param>
    /// <param name="createBranch">Opens the create branch window on a commit, true when a branch was created.</param>
    public VerifyDatabaseWindow(string repositoryPath, Func<string, Task<bool>> runGitWithOutput,
        Func<Window, ObjectId, Task<bool>> createTag, Func<Window, ObjectId, Task<bool>> createBranch)
    {
        _module = GitModules.Open(repositoryPath);
        _runGitWithOutput = runGitWithOutput;
        _createTag = createTag;
        _createBranch = createBranch;
        InitializeComponent();
        UpstreamTranslation.Apply(this, "FormVerify");
        InitialFocus.Attach(this, () => Warnings);
        Warnings.ItemTemplate = new FuncDataTemplate<LostObjectRow>((row, _) => Row(row));
        Warnings.SelectionChanged += (_, _) => _previewShown = ShowSelectedAsync();
        Warnings.DoubleTapped += (_, e) =>
        {
            // Upstream ignores a double click on the check box, which the user meant to toggle.
            if ((e.Source as Visual)?.FindAncestorOfType<CheckBox>(includeSelf: true) is null)
            {
                Run(ViewCurrentItemAsync);
            }
        };
        Warnings.AddHandler(KeyDownEvent, OnWarningsKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);
        mnuLostObjects.Opening += (_, e) => e.Cancel = !EnableMenuItems();
        mnuLostObjectView.Click += (_, _) => Run(ViewCurrentItemAsync);
        mnuLostObjectsCreateTag.Click += (_, _) => Run(CreateTagAsync);
        mnuLostObjectsCreateBranch.Click += (_, _) => Run(CreateBranchAsync);
        copyHashToolStripMenuItem.Click += (_, _) => Run(() => CopyAsync(row => row.Object.ObjectId));
        copyParentHashToolStripMenuItem.Click += (_, _) => Run(() => CopyAsync(row => row.Object.Parent));
        saveAsToolStripMenuItem.Click += (_, _) => Run(SaveAsAsync);

        foreach ((Button header, int column) in new[]
                 {
                     (columnDate, DateColumn), (columnType, TypeColumn), (columnSubject, SubjectColumn),
                     (columnAuthor, AuthorColumn), (columnHash, HashColumn), (columnParent, ParentColumn),
                 })
        {
            header.Click += (_, _) => SortBy(column);
        }

        columnIsLostObjectSelected.IsCheckedChanged += (_, _) => SelectAll(columnIsLostObjectSelected.IsChecked == true);
        ShowCommitsAndTags.IsCheckedChanged += (_, _) => OnShowFilterChanged(ShowCommitsAndTags, ShowOtherObjects);
        ShowOtherObjects.IsCheckedChanged += (_, _) => OnShowFilterChanged(ShowOtherObjects, ShowCommitsAndTags);
        NoReflogs.IsCheckedChanged += (_, _) => Reload();
        Unreachable.IsCheckedChanged += (_, _) => Reload();
        FullCheck.IsCheckedChanged += (_, _) => Reload();
        SaveObjects.Click += (_, _) => Run(SaveObjectsAsync);
        Remove.Click += (_, _) => Run(RemoveAsync);
        DeleteAllLostAndFoundTags.Click += (_, _) => Run(DeleteAllLostAndFoundTagsAsync);
        btnRestoreSelectedObjects.Click += (_, _) => Run(RestoreSelectedObjectsAsync);
        btnCloseDialog.Click += (_, _) => Close(false);
        Opened += (_, _) => Reload();
    }

    /// <summary>
    ///  The last read of the lost objects, for tests.
    /// </summary>
    public Task ObjectsLoaded => _objectsLoaded;

    /// <summary>
    ///  The last preview of the selected object, for tests.
    /// </summary>
    public Task PreviewShown => _previewShown;

    /// <summary>
    ///  The objects as listed (after the "Show ..." filters, in the sort order).
    /// </summary>
    public IReadOnlyList<LostObjectRow> Rows => Warnings.ItemsSource as IReadOnlyList<LostObjectRow> ?? [];

    private LostObjectRow? CurrentItem => Warnings.SelectedItem as LostObjectRow;

    // Upstream's UpdateLostObjects, run on Shown and when an fsck option changes; the window waits while git checks.
    private void Reload() => Run(() => _objectsLoaded = LoadAsync());

    private async Task LoadAsync()
    {
        string options = VerifyDatabase.Options(Unreachable.IsChecked == true, FullCheck.IsChecked == true,
            NoReflogs.IsChecked == true);
        IsEnabled = false;
        Cursor = new Cursor(StandardCursorType.Wait);
        try
        {
            IReadOnlyList<LostObject> lostObjects = await Task.Run(() => VerifyDatabase.Load(_module, options));
            _rows = [.. lostObjects.Select(lostObject => new LostObjectRow(lostObject))];
            _typeDetected = false;
            _previewed = null;
            ShowRows();
        }
        finally
        {
            Cursor = Cursor.Default;
            IsEnabled = true;
        }
    }

    // Upstream's ShowCommitsCheckedChanged and ShowOtherObjects_CheckedChanged: one of the two stays checked.
    private void OnShowFilterChanged(CheckBox changed, CheckBox other)
    {
        if (changed.IsChecked != true && other.IsChecked != true)
        {
            other.IsChecked = true;
        }
        else
        {
            ShowRows();
        }
    }

    // Upstream's UpdateFilteredLostObjects: the filtered rows in the sort order, the commit columns only with commits;
    // the first time blobs are shown, their type is guessed from their content in the background.
    private void ShowRows()
    {
        bool commitsAndTags = ShowCommitsAndTags.IsChecked == true;
        bool others = ShowOtherObjects.IsChecked == true;
        if (others && !_typeDetected)
        {
            _typeDetected = true;
            Run(DetectBlobTypesAsync);
        }

        IEnumerable<LostObjectRow> rows = _rows.Where(row => VerifyDatabase.IsShown(row.Object, commitsAndTags, others));
        Func<LostObjectRow, IComparable?> key = row => SortKey(row.Object, _sortColumn);
        rows = _descending ? rows.OrderByDescending(key) : rows.OrderBy(key);
        Warnings.ItemsSource = rows.ToList();

        foreach (Control header in new Control[] { columnSubject, columnAuthor, columnParent })
        {
            header.IsVisible = commitsAndTags;
        }

        WarningsHeader.ColumnDefinitions[AuthorColumn].Width = new GridLength(commitsAndTags ? AuthorColumnWidth : 0);
        WarningsHeader.ColumnDefinitions[ParentColumn].Width = new GridLength(commitsAndTags ? HashColumnWidth : 0);
    }

    private async Task DetectBlobTypesAsync()
    {
        LostObject[] blobs = [.. _rows.Select(row => row.Object).Where(o => o.ObjectType == LostObjectType.Blob)];
        await Task.Run(() =>
        {
            foreach (LostObject blob in blobs)
            {
                blob.RawType = VerifyDatabase.WithSeemingType(blob.RawType, VerifyDatabase.Content(_module, blob));
            }
        });
        ShowRows();
    }

    // Upstream's SortableLostObjectsList: a header click sorts by its column, a second click the other way.
    private void SortBy(int column)
    {
        _descending = _sortColumn == column && !_descending;
        _sortColumn = column;
        ShowRows();
    }

    private static IComparable? SortKey(LostObject lostObject, int column)
        => column switch
        {
            DateColumn => lostObject.Date,
            TypeColumn => lostObject.RawType,
            SubjectColumn => lostObject.Subject,
            AuthorColumn => lostObject.Author,
            HashColumn => lostObject.ObjectId.ToString(),
            _ => lostObject.Parent.ToString()
        };

    private void SelectAll(bool selected)
    {
        foreach (LostObjectRow row in Rows)
        {
            row.IsSelected = selected;
        }
    }

    // Upstream's Warnings_SelectionChanged: a commit or tag as a patch, a blob as a file of its guessed type, the rest as
    // text; the name is kept for View and Save as.
    private async Task ShowSelectedAsync()
    {
        _defaultFileName = null;
        if (CurrentItem is not { } row || row == _previewed)
        {
            return;
        }

        _previewed = row;
        string content = await Task.Run(() => VerifyDatabase.Content(_module, row.Object));
        if (CurrentItem != row)
        {
            return;
        }

        _defaultFileName = VerifyDatabase.DefaultFileName(row.Object, content);
        if (row.Object.IsCommitOrTag)
        {
            fileViewer.ShowPatch(_defaultFileName, content);
        }
        else
        {
            fileViewer.ShowText(_defaultFileName, content);
        }
    }

    // Upstream's mnuLostObjects_Opening: tag, branch and parent hash for a commit, Save as for a blob.
    private bool EnableMenuItems()
    {
        if (CurrentItem is not { } row)
        {
            return false;
        }

        bool isCommit = row.Object.ObjectType == LostObjectType.Commit;
        mnuLostObjectsCreateTag.IsEnabled = isCommit;
        mnuLostObjectsCreateBranch.IsEnabled = isCommit;
        copyParentHashToolStripMenuItem.IsEnabled = isCommit;
        saveAsToolStripMenuItem.IsEnabled = row.Object.ObjectType == LostObjectType.Blob;
        return true;
    }

    // Upstream's Warnings_KeyDown: Enter views the object.
    private void OnWarningsKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None)
        {
            e.Handled = true;
            Run(ViewCurrentItemAsync);
        }
    }

    // Upstream's ProcessKeyPreview: the menu's shortcuts work while the menu is closed, for the items it would enable.
    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.Control || !EnableMenuItems())
        {
            return;
        }

        MenuItem? item = e.Key switch
        {
            Key.T => mnuLostObjectsCreateTag,
            Key.B => mnuLostObjectsCreateBranch,
            Key.S => saveAsToolStripMenuItem,
            _ => null
        };
        if (item is { IsEnabled: true })
        {
            e.Handled = true;
            item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        }
    }

    // Upstream's ViewCurrentItem: the content read-only in upstream's FormEdit.
    private async Task ViewCurrentItemAsync()
    {
        if (CurrentItem is not { } row)
        {
            return;
        }

        string content = await Task.Run(() => VerifyDatabase.Content(_module, row.Object));
        if (!string.IsNullOrEmpty(content))
        {
            await new ViewTextWindow(content, _defaultFileName ?? VerifyDatabase.DefaultFileName(row.Object, content))
                .ShowDialog(this);
        }
    }

    private async Task CreateTagAsync()
    {
        if (CurrentItem is { } row && await _createTag(this, row.Object.ObjectId))
        {
            Reload();
        }
    }

    private async Task CreateBranchAsync()
    {
        if (CurrentItem is { } row && await _createBranch(this, row.Object.ObjectId))
        {
            Reload();
        }
    }

    // Upstream's copy items; a zero parent (no parent) is not copied.
    private async Task CopyAsync(Func<LostObjectRow, ObjectId> id)
    {
        if (CurrentItem is { } row && id(row) is { IsZero: false } objectId && Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(objectId.ToString());
        }
    }

    // Upstream's saveAsToolStripMenuItem_Click: a blob saved under its guessed name, offering its type's equivalents.
    private async Task SaveAsAsync()
    {
        if (CurrentItem is not { Object.ObjectType: LostObjectType.Blob } row)
        {
            return;
        }

        string fileName = VerifyDatabase.SaveAsFileName(row.Object, _defaultFileName);
        IReadOnlyList<string> extensions = VerifyDatabase.SaveAsExtensions(fileName);
        IStorageFile? file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(_module.WorkingDir),
            SuggestedFileName = fileName,
            DefaultExtension = extensions[0],
            FileTypeChoices =
            [
                .. extensions.Select(extension => new FilePickerFileType($"{extension} Files")
                {
                    Patterns = [$"*.{extension}"],
                }),
                new FilePickerFileType("All Files") { Patterns = ["*.*"] },
            ],
        });
        if (file?.TryGetLocalPath() is { } path)
        {
            await Task.Run(() => _module.SaveBlobAs(path, row.Object.ObjectId.ToString()));
        }
    }

    // Upstream's SaveObjectsClick.
    private async Task SaveObjectsAsync()
    {
        string options = VerifyDatabase.Options(Unreachable.IsChecked == true, FullCheck.IsChecked == true,
            NoReflogs.IsChecked == true);
        await _runGitWithOutput(VerifyDatabase.SaveObjectsArguments(options));
        Reload();
    }

    // Upstream's RemoveClick: after its question, git prune.
    private async Task RemoveAsync()
    {
        MessageBoxWindow question = new(UpstreamTranslation.ByText(VerifyDatabase.RemoveDanglingObjectsQuestion),
            UpstreamTranslation.ByText(VerifyDatabase.RemoveDanglingObjectsCaption), MessageBoxButtons.YesNo);
        await question.ShowDialog(this);
        if (question.Result != DialogResult.Yes)
        {
            return;
        }

        await _runGitWithOutput(VerifyDatabase.RemoveArguments);
        Reload();
    }

    private async Task DeleteAllLostAndFoundTagsAsync()
    {
        await Task.Run(() => VerifyDatabase.DeleteLostFoundTags(_module));
        Reload();
    }

    // Upstream's btnRestoreSelectedObjects_Click: the old LOST_FOUND_ tags deleted, one on each checked object; when all
    // listed objects are recovered, the window closes so the user sees them in the browse window.
    private async Task RestoreSelectedObjectsAsync()
    {
        IReadOnlyList<LostObjectRow> listed = Rows;
        LostObject[] selected = [.. listed.Where(row => row.IsSelected).Select(row => row.Object)];
        await Task.Run(() => VerifyDatabase.DeleteLostFoundTags(_module));
        if (selected.Length == 0)
        {
            await new MessageBoxWindow(UpstreamTranslation.ByText(VerifyDatabase.SelectLostObjectsToRestoreMessage),
                UpstreamTranslation.ByText(VerifyDatabase.SelectLostObjectsToRestoreCaption), MessageBoxButtons.OK)
                .ShowDialog(this);
            return;
        }

        int restored = await Task.Run(() => VerifyDatabase.CreateLostFoundTags(_module, selected));
        await new MessageBoxWindow(string.Format(UpstreamTranslation.ByText(VerifyDatabase.XTagsCreated), restored),
            VerifyDatabase.TagsCreatedCaption, MessageBoxButtons.OK).ShowDialog(this);
        if (restored == listed.Count)
        {
            Close(true);
            return;
        }

        Reload();
    }

    private Control Row(LostObjectRow? row)
    {
        Grid grid = new()
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(24, GridUnitType.Pixel),
                new ColumnDefinition(GridLength.Auto) { SharedSizeGroup = "Date" },
                new ColumnDefinition(GridLength.Auto) { SharedSizeGroup = "Type" },
                new ColumnDefinition(1, GridUnitType.Star),
                new ColumnDefinition(ShowCommitsAndTags.IsChecked == true ? AuthorColumnWidth : 0, GridUnitType.Pixel),
                new ColumnDefinition(HashColumnWidth, GridUnitType.Pixel),
                new ColumnDefinition(ShowCommitsAndTags.IsChecked == true ? HashColumnWidth : 0, GridUnitType.Pixel),
            },
        };
        if (row is null)
        {
            return grid;
        }

        CheckBox check = new() { MinWidth = 0, Padding = new(0), VerticalAlignment = VerticalAlignment.Top, DataContext = row };
        check.Bind(CheckBox.IsCheckedProperty, new Binding(nameof(LostObjectRow.IsSelected)) { Mode = BindingMode.TwoWay });
        grid.Children.Add(check);
        LostObject lostObject = row.Object;
        AddCell(DateColumn, lostObject.Date?.ToString() ?? "", wrap: false);
        AddCell(TypeColumn, lostObject.RawType, wrap: false);
        AddCell(SubjectColumn, lostObject.Subject ?? "", wrap: false);
        AddCell(AuthorColumn, lostObject.Author ?? "", wrap: false);
        AddCell(HashColumn, lostObject.ObjectId.ToString(), wrap: true);
        AddCell(ParentColumn, lostObject.Parent.IsZero ? "" : lostObject.Parent.ToString(), wrap: true);
        return grid;

        void AddCell(int column, string text, bool wrap)
        {
            TextBlock cell = new()
            {
                Text = text,
                Margin = new(0, 0, 6, 0),
                TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
                TextTrimming = wrap ? TextTrimming.None : TextTrimming.CharacterEllipsis,
            };
            Grid.SetColumn(cell, column);
            grid.Children.Add(cell);
        }
    }

    private void Run(Func<Task> action)
        => UiActions.Run(action, ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));
}

/// <summary>
///  A row of the lost objects list: the object and whether its check box (upstream's first column) is checked.
/// </summary>
public sealed class LostObjectRow(LostObject lostObject) : ObservableObject
{
    private bool _isSelected;

    public LostObject Object { get; } = lostObject;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
