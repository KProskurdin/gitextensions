namespace GitExtensions.Xplat.Core.CommitHistory;

/// <summary>
///  The files of one commit, and the history of the file that is selected. Holds no UI types.
/// </summary>
public sealed class FileBrowserViewModel : ObservableObject
{
    private readonly ICommitHistory _history;
    private string? _repositoryPath;
    private string? _hash;
    private int _openVersion;
    private int _historyVersion;
    private IReadOnlyList<string> _files = [];
    private string? _selectedFile;
    private IReadOnlyList<CommitRow> _fileHistory = [];
    private string? _errorMessage;

    public FileBrowserViewModel(ICommitHistory history)
    {
        _history = history;
    }

    public IReadOnlyList<string> Files
    {
        get => _files;
        private set => SetProperty(ref _files, value);
    }

    public string? SelectedFile
    {
        get => _selectedFile;
        private set => SetProperty(ref _selectedFile, value);
    }

    /// <summary>
    ///  Commits that changed <see cref="SelectedFile"/>, newest first.
    /// </summary>
    public IReadOnlyList<CommitRow> FileHistory
    {
        get => _fileHistory;
        private set => SetProperty(ref _fileHistory, value);
    }

    /// <summary>
    ///  Set when the tree or a history cannot be read. A view shows it and then calls <see cref="ClearError"/>.
    /// </summary>
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public void ClearError() => ErrorMessage = null;

    public async Task OpenAsync(string repositoryPath, string hash)
    {
        int version = ++_openVersion;
        _repositoryPath = repositoryPath;
        _hash = hash;
        SelectedFile = null;
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
    }

    public async Task SelectFileAsync(string? filePath)
    {
        int version = ++_historyVersion;
        SelectedFile = filePath;
        if (filePath is null || _repositoryPath is null || _hash is null)
        {
            FileHistory = [];
            return;
        }

        try
        {
            CommitPage page = await _history.LoadFileHistoryAsync(_repositoryPath, _hash, filePath, CommitListViewModel.PageSize);
            if (version == _historyVersion)
            {
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
}
