using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitCommands;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Presentation;

namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  The view model of upstream's <c>FormVerify</c> (Repository > Git maintenance > Recover lost objects...; rules in
///  <see cref="VerifyDatabase"/>): the objects <c>git fsck-objects</c> finds with the chosen options, their content, and
///  upstream's ways to recover them: LOST_FOUND_ tags on the checked ones, a tag or branch on one, saving a blob, or
///  saving them all to .git/lost-found. Closes its window with true when all listed objects were recovered.
/// </summary>
public sealed partial class VerifyDatabaseViewModel : DialogViewModel
{
    /// <summary>
    ///  The upstream form whose texts the window shows, by their names.
    /// </summary>
    public const string Category = "FormVerify";

    // Upstream's column order: the check box, then Date, Type, Subject, Author, Hash and Parent.
    public const int DateColumn = 1;
    public const int TypeColumn = 2;
    public const int SubjectColumn = 3;
    public const int AuthorColumn = 4;
    public const int HashColumn = 5;
    public const int ParentColumn = 6;

    private readonly GitModule _module;
    private readonly Func<string, Task<bool>> _runGitWithOutput;
    private readonly Func<ObjectId, Task<bool>> _createTag;
    private readonly Func<ObjectId, Task<bool>> _createBranch;
    private IReadOnlyList<LostObjectRow> _allRows = [];
    private int _sortColumn = DateColumn;
    private bool _descending = true;
    private bool _typeDetected;
    private LostObjectRow? _previewed;

    /// <param name="module">The repository whose objects are checked.</param>
    /// <param name="runGitWithOutput">Runs a git command with its output shown (upstream's FormProcess), for "Save
    ///  objects to .git/lost-found" and "Remove all dangling objects".</param>
    /// <param name="createTag">Opens the create tag window on an object, true when a tag was created.</param>
    /// <param name="createBranch">Opens the create branch window on a commit, true when a branch was created.</param>
    public VerifyDatabaseViewModel(IDialogHost host, GitModule module, Func<string, Task<bool>> runGitWithOutput,
        Func<ObjectId, Task<bool>> createTag, Func<ObjectId, Task<bool>> createBranch)
        : base(host)
    {
        _module = module;
        _runGitWithOutput = runGitWithOutput;
        _createTag = createTag;
        _createBranch = createBranch;
    }

    [ObservableProperty]

    public partial bool ShowCommitsAndTags { get; set; } = true;

    [ObservableProperty]

    public partial bool ShowOtherObjects { get; set; }

    [ObservableProperty]

    public partial bool NoReflogs { get; set; } = true;

    [ObservableProperty]

    public partial bool Unreachable { get; set; }

    [ObservableProperty]

    public partial bool FullCheck { get; set; }

    /// <summary>
    ///  The header's check box, which checks or unchecks every listed object.
    /// </summary>
    [ObservableProperty]
    public partial bool AllSelected { get; set; }

    /// <summary>
    ///  Git checks the objects; the window waits.
    /// </summary>
    [ObservableProperty]
    public partial bool IsLoading { get; private set; }

    /// <summary>
    ///  The objects as listed (after the "Show ..." filters, in the sort order).
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<LostObjectRow> Rows { get; private set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedIsCommit), nameof(SelectedIsBlob))]
    public partial LostObjectRow? SelectedRow { get; set; }

    /// <summary>
    ///  Upstream's menu: tag, branch and parent hash for a commit.
    /// </summary>
    public bool SelectedIsCommit => SelectedRow?.Object.ObjectType == LostObjectType.Commit;

    /// <summary>
    ///  Upstream's menu: Save as for a blob.
    /// </summary>
    public bool SelectedIsBlob => SelectedRow?.Object.ObjectType == LostObjectType.Blob;

    /// <summary>
    ///  The selected object's content and name, as the viewer shows it; null for none.
    /// </summary>
    [ObservableProperty]
    public partial LostObjectPreview? Preview { get; private set; }

    /// <summary>
    ///  The last read of the lost objects, for tests.
    /// </summary>
    public Task ObjectsLoaded { get; private set; } = Task.CompletedTask;

    /// <summary>
    ///  The last preview of the selected object, for tests.
    /// </summary>
    public Task PreviewShown { get; private set; } = Task.CompletedTask;

    /// <summary>
    ///  Shows a text read-only (upstream's FormEdit) with its file name; the window sets it, as it opens a window.
    /// </summary>
    public Func<string, string, Task>? ViewText { get; set; }

    /// <summary>
    ///  Copies a text to the clipboard; the window sets it, as the clipboard is the platform's.
    /// </summary>
    public Func<string, Task>? CopyText { get; set; }

    /// <summary>
    ///  Picks where to save a blob, from its name and its type's extensions; the window sets it.
    /// </summary>
    public Func<string, IReadOnlyList<string>, Task<string?>>? PickSaveFile { get; set; }

    /// <summary>
    ///  Upstream's <c>UpdateLostObjects</c>, run on Shown and when an fsck option changes.
    /// </summary>
    public void Reload() => Run(() => ObjectsLoaded = LoadAsync());

    partial void OnShowCommitsAndTagsChanged(bool value) => OnShowFilterChanged(value, () => ShowOtherObjects = true);

    partial void OnShowOtherObjectsChanged(bool value) => OnShowFilterChanged(value, () => ShowCommitsAndTags = true);

    partial void OnNoReflogsChanged(bool value) => Reload();

    partial void OnUnreachableChanged(bool value) => Reload();

    partial void OnFullCheckChanged(bool value) => Reload();

    partial void OnAllSelectedChanged(bool value)
    {
        foreach (LostObjectRow row in Rows)
        {
            row.IsSelected = value;
        }
    }

    partial void OnSelectedRowChanged(LostObjectRow? value) => PreviewShown = ShowSelectedAsync(value);

    private string Options() => VerifyDatabase.Options(Unreachable, FullCheck, NoReflogs);

    private async Task LoadAsync()
    {
        string options = Options();
        IsLoading = true;
        try
        {
            IReadOnlyList<LostObject> lostObjects = await Task.Run(() => VerifyDatabase.Load(_module, options));
            _allRows = [.. lostObjects.Select(lostObject => new LostObjectRow(lostObject))];
            _typeDetected = false;
            _previewed = null;
            ShowRows();
        }
        finally
        {
            IsLoading = false;
        }
    }

    // Upstream's ShowCommitsCheckedChanged and ShowOtherObjects_CheckedChanged: one of the two stays checked.
    private void OnShowFilterChanged(bool value, Action checkOther)
    {
        if (!value && !ShowCommitsAndTags && !ShowOtherObjects)
        {
            checkOther();
        }
        else
        {
            ShowRows();
        }
    }

    // Upstream's UpdateFilteredLostObjects: the filtered rows in the sort order; the first time blobs are shown, their
    // type is guessed from their content in the background.
    private void ShowRows()
    {
        bool commitsAndTags = ShowCommitsAndTags;
        bool others = ShowOtherObjects;
        if (others && !_typeDetected)
        {
            _typeDetected = true;
            Run(DetectBlobTypesAsync);
        }

        IEnumerable<LostObjectRow> rows =
            _allRows.Where(row => VerifyDatabase.IsShown(row.Object, commitsAndTags, others));
        Func<LostObjectRow, IComparable?> key = row => SortKey(row.Object, _sortColumn);
        rows = _descending ? rows.OrderByDescending(key) : rows.OrderBy(key);
        Rows = [.. rows];
    }

    private async Task DetectBlobTypesAsync()
    {
        LostObject[] blobs = [.. _allRows.Select(row => row.Object).Where(o => o.ObjectType == LostObjectType.Blob)];
        await Task.Run(() =>
        {
            foreach (LostObject blob in blobs)
            {
                blob.RawType = VerifyDatabase.WithSeemingType(blob.RawType, VerifyDatabase.Content(_module, blob));
            }
        });
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

    // Upstream's Warnings_SelectionChanged: a commit or tag as a patch, a blob as a file of its guessed type, the rest as
    // text; the name is kept for View and Save as.
    private async Task ShowSelectedAsync(LostObjectRow? row)
    {
        if (row is null || row == _previewed)
        {
            return;
        }

        _previewed = row;
        string content = await Task.Run(() => VerifyDatabase.Content(_module, row.Object));
        if (SelectedRow != row)
        {
            return;
        }

        Preview = new LostObjectPreview(VerifyDatabase.DefaultFileName(row.Object, content), content,
            row.Object.IsCommitOrTag);
    }

    // Upstream's SortableLostObjectsList: a header click sorts by its column, a second click the other way.
    [RelayCommand]
    private void Sort(int column)
    {
        _descending = _sortColumn == column && !_descending;
        _sortColumn = column;
        ShowRows();
    }

    // Upstream's ViewCurrentItem: the content read-only in upstream's FormEdit.
    [RelayCommand]
    private Task ViewAsync() => GuardAsync(async () =>
    {
        if (SelectedRow is not { } row)
        {
            return;
        }

        string content = await Task.Run(() => VerifyDatabase.Content(_module, row.Object));
        if (!string.IsNullOrEmpty(content) && ViewText is not null)
        {
            string fileName = Preview is { } preview && _previewed == row
                ? preview.FileName
                : VerifyDatabase.DefaultFileName(row.Object, content);
            await ViewText(content, fileName);
        }
    });

    [RelayCommand]
    private Task CreateTagAsync() => GuardAsync(async () =>
    {
        if (SelectedRow is { } row && await _createTag(row.Object.ObjectId))
        {
            Reload();
        }
    });

    [RelayCommand]
    private Task CreateBranchAsync() => GuardAsync(async () =>
    {
        if (SelectedRow is { } row && await _createBranch(row.Object.ObjectId))
        {
            Reload();
        }
    });

    // Upstream's copy items; a zero parent (no parent) is not copied.
    [RelayCommand]
    private Task CopyHashAsync() => GuardAsync(() => CopyAsync(row => row.Object.ObjectId));

    [RelayCommand]
    private Task CopyParentHashAsync() => GuardAsync(() => CopyAsync(row => row.Object.Parent));

    private async Task CopyAsync(Func<LostObjectRow, ObjectId> id)
    {
        if (SelectedRow is { } row && id(row) is { IsZero: false } objectId && CopyText is not null)
        {
            await CopyText(objectId.ToString());
        }
    }

    // Upstream's saveAsToolStripMenuItem_Click: a blob saved under its guessed name, offering its type's equivalents.
    [RelayCommand]
    private Task SaveAsAsync() => GuardAsync(async () =>
    {
        if (SelectedRow is not { Object.ObjectType: LostObjectType.Blob } row || PickSaveFile is null)
        {
            return;
        }

        string? defaultFileName = _previewed == row ? Preview?.FileName : null;
        string fileName = VerifyDatabase.SaveAsFileName(row.Object, defaultFileName);
        if (await PickSaveFile(fileName, VerifyDatabase.SaveAsExtensions(fileName)) is { } path)
        {
            await Task.Run(() => _module.SaveBlobAs(path, row.Object.ObjectId.ToString()));
        }
    });

    // Upstream's SaveObjectsClick.
    [RelayCommand]
    private Task SaveObjectsAsync() => GuardAsync(async () =>
    {
        await _runGitWithOutput(VerifyDatabase.SaveObjectsArguments(Options()));
        Reload();
    });

    // Upstream's RemoveClick: after its question, git prune.
    [RelayCommand]
    private Task RemoveAsync() => GuardAsync(async () =>
    {
        if (!await Host.ConfirmAsync(Host.Translate(VerifyDatabase.RemoveDanglingObjectsQuestion),
                Host.Translate(VerifyDatabase.RemoveDanglingObjectsCaption)))
        {
            return;
        }

        await _runGitWithOutput(VerifyDatabase.RemoveArguments);
        Reload();
    });

    [RelayCommand]
    private Task DeleteAllLostAndFoundTagsAsync() => GuardAsync(async () =>
    {
        await Task.Run(() => VerifyDatabase.DeleteLostFoundTags(_module));
        Reload();
    });

    // Upstream's btnRestoreSelectedObjects_Click: the old LOST_FOUND_ tags deleted, one on each checked object; when all
    // listed objects are recovered, the window closes so the user sees them in the browse window.
    [RelayCommand]
    private Task RestoreSelectedObjectsAsync() => GuardAsync(async () =>
    {
        IReadOnlyList<LostObjectRow> listed = Rows;
        LostObject[] selected = [.. listed.Where(row => row.IsSelected).Select(row => row.Object)];
        await Task.Run(() => VerifyDatabase.DeleteLostFoundTags(_module));
        if (selected.Length == 0)
        {
            await Host.ShowMessageAsync(Host.Translate(VerifyDatabase.SelectLostObjectsToRestoreMessage),
                Host.Translate(VerifyDatabase.SelectLostObjectsToRestoreCaption));
            return;
        }

        int restored = await Task.Run(() => VerifyDatabase.CreateLostFoundTags(_module, selected));
        await Host.ShowMessageAsync(string.Format(Host.Translate(VerifyDatabase.XTagsCreated), restored),
            VerifyDatabase.TagsCreatedCaption);
        if (restored == listed.Count)
        {
            Host.Close(true);
            return;
        }

        Reload();
    });

    [RelayCommand]
    private void Cancel() => Host.Close(false);
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

/// <summary>
///  A lost object's content as the viewer shows it: a patch for a commit or tag, else a file of the given name.
/// </summary>
public sealed record LostObjectPreview(string FileName, string Content, bool IsPatch);
