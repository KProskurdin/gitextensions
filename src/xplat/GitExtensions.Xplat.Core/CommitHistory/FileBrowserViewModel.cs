using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.Core.CommitHistory;

/// <summary>
///  The files of one commit, and the history of the file that is selected, with upstream FormFileHistory's rename choices.
///  Holds no UI types; the files window binds to it (PLAN.md principle 6).
/// </summary>
public sealed partial class FileBrowserViewModel : ObservableObject
{
    private const int ShortHashLength = 8;

    private readonly ICommitHistory _history;
    private readonly IAppPreferences? _preferences;
    private string? _repositoryPath;
    private string? _hash;
    private int _openVersion;
    private int _historyVersion;
    private IReadOnlyList<string> _files = [];
    private string? _selectedFile;
    private IReadOnlyList<CommitRow> _fileHistory = [];
    private string? _errorMessage;
    private IReadOnlyDictionary<string, string>? _filePaths;

    /// <param name="preferences">Where upstream's file history settings are kept; without it the rename choices are
    /// upstream's defaults and are not stored.</param>
    public FileBrowserViewModel(ICommitHistory history, IAppPreferences? preferences = null)
    {
        _history = history;
        _preferences = preferences;
        FileHistoryOptions options = preferences?.FileHistory ?? new FileHistoryOptions();
        FollowRenames = options.FollowRenames;
        ExactRenamesOnly = options.ExactRenamesOnly;
    }

    /// <summary>
    ///  The window's title: the commit whose files are listed.
    /// </summary>
    public string Title => _hash is null ? "Files" : $"Files at {_hash[..Math.Min(ShortHashLength, _hash.Length)]}";

    public IReadOnlyList<string> Files
    {
        get => _files;
        private set => SetProperty(ref _files, value);
    }

    /// <summary>
    ///  The file whose history shows; setting it reads that history.
    /// </summary>
    public string? SelectedFile
    {
        get => _selectedFile;
        set
        {
            if (value != _selectedFile)
            {
                _ = SelectFileAsync(value);
            }
        }
    }

    /// <summary>
    ///  Commits that changed <see cref="SelectedFile"/>, newest first.
    /// </summary>
    public IReadOnlyList<CommitRow> FileHistory
    {
        get => _fileHistory;
        private set => SetProperty(ref _fileHistory, value);
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ShowHistoryDiffCommand))]
    public partial CommitRow? SelectedHistoryRow { get; set; }

    /// <summary>
    ///  Upstream's "Detect and follow renames" (<c>FollowRenamesInFileHistory</c>).
    /// </summary>
    [ObservableProperty]
    public partial bool FollowRenames { get; set; }

    /// <summary>
    ///  Upstream's "Detect and follow - exact renames and copies only", which needs <see cref="FollowRenames"/>.
    /// </summary>
    [ObservableProperty]
    public partial bool ExactRenamesOnly { get; set; }

    /// <summary>
    ///  Set when the tree or a history cannot be read; it shows until the next error.
    /// </summary>
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    /// <summary>
    ///  Opens the blame of a file at the commit; the window sets it, as it opens another window.
    /// </summary>
    public Action<string>? OpenBlame { get; set; }

    /// <summary>
    ///  Opens the diff of a file (its path then) in a commit; the window sets it, as it opens another window.
    /// </summary>
    public Action<string, string>? OpenDiff { get; set; }

    private bool HasSelectedFile => SelectedFile is not null;

    private bool HasSelectedHistoryRow => SelectedFile is not null && SelectedHistoryRow is not null;

    public void ClearError() => ErrorMessage = null;

    /// <summary>
    ///  The path the selected file had in <paramref name="row"/>'s commit: an earlier name when the history followed a rename.
    /// </summary>
    public string? PathIn(CommitRow row)
        => _filePaths?.GetValueOrDefault(row.Hash) ?? SelectedFile;

    /// <summary>
    ///  Reads the selected file's history again, e.g. after the rename options changed.
    /// </summary>
    public Task ReloadHistoryAsync() => SelectFileAsync(SelectedFile);

    /// <param name="selectFile">A file to select once the files are read, whose history then shows.</param>
    public async Task OpenAsync(string repositoryPath, string hash, string? selectFile = null)
    {
        int version = ++_openVersion;
        _repositoryPath = repositoryPath;
        _hash = hash;
        OnPropertyChanged(nameof(Title));
        SetSelectedFile(null);
        FileHistory = [];
        try
        {
            IReadOnlyList<string> files = await _history.LoadTreeAsync(repositoryPath, hash);
            if (version == _openVersion)
            {
                Files = files;
            }
        }
        catch (Exception ex)
        {
            if (version == _openVersion)
            {
                Files = [];
                ErrorMessage = ex.Message;
            }
        }

        if (selectFile is not null && version == _openVersion)
        {
            await SelectFileAsync(selectFile);
        }
    }

    public async Task SelectFileAsync(string? filePath)
    {
        int version = ++_historyVersion;
        SetSelectedFile(filePath);
        if (filePath is null || _repositoryPath is null || _hash is null)
        {
            FileHistory = [];
            return;
        }

        try
        {
            CommitPage page =
                await _history.LoadFileHistoryAsync(_repositoryPath, _hash, filePath, CommitListViewModel.PageSize);
            if (version == _historyVersion)
            {
                _filePaths = page.FilePaths;
                FileHistory = page.Rows;
            }
        }
        catch (Exception ex)
        {
            if (version == _historyVersion)
            {
                FileHistory = [];
                ErrorMessage = ex.Message;
            }
        }
    }

    // As upstream's menu items: the setting changes for every file history, and this one is read again.
    partial void OnFollowRenamesChanged(bool value) => StoreRenameOptions();

    partial void OnExactRenamesOnlyChanged(bool value) => StoreRenameOptions();

    private void StoreRenameOptions()
    {
        if (_preferences is null
            || (_preferences.FileHistory.FollowRenames == FollowRenames
                && _preferences.FileHistory.ExactRenamesOnly == ExactRenamesOnly))
        {
            return;
        }

        _preferences.FileHistory = _preferences.FileHistory with
        {
            FollowRenames = FollowRenames,
            ExactRenamesOnly = ExactRenamesOnly,
        };
        _ = ReloadHistoryAsync();
    }

    private void SetSelectedFile(string? filePath)
    {
        if (SetProperty(ref _selectedFile, filePath, nameof(SelectedFile)))
        {
            BlameCommand.NotifyCanExecuteChanged();
            ShowHistoryDiffCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedFile))]
    private void Blame()
    {
        if (SelectedFile is { } file)
        {
            OpenBlame?.Invoke(file);
        }
    }

    // The file's name in that commit, which may be an earlier one when the history follows renames.
    [RelayCommand(CanExecute = nameof(HasSelectedHistoryRow))]
    private void ShowHistoryDiff()
    {
        if (SelectedFile is { } file && SelectedHistoryRow is { } row)
        {
            OpenDiff?.Invoke(row.Hash, PathIn(row) ?? file);
        }
    }
}
