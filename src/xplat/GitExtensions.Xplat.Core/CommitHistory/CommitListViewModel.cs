using System.IO;

namespace GitExtensions.Xplat.Core.CommitHistory;

/// <summary>
///  State of the commit list and the selected commit's details. Holds no UI types: a view observes the properties and
///  forwards user actions. Create and use it on the UI thread; results of background reads are applied after an await.
/// </summary>
public sealed class CommitListViewModel : ObservableObject
{
    public const int PageSize = 500;

    private readonly ICommitHistory _history;
    private string? _repositoryPath;
    private int _pages;
    private int _loadVersion;
    private int _detailsVersion;
    private IReadOnlyList<CommitRow> _rows = [];
    private CommitRow? _selected;
    private CommitDetails? _details;
    private string _status = "";
    private string _repositoryName = "";
    private bool _hasMore;
    private bool _isLoading;
    private string? _errorMessage;
    private string? _detailsError;

    public CommitListViewModel(ICommitHistory history)
    {
        _history = history;
    }

    /// <summary>
    ///  The repository whose history is shown, or null when none could be opened.
    /// </summary>
    public string? RepositoryPath => _repositoryPath;

    public IReadOnlyList<CommitRow> Rows
    {
        get => _rows;
        private set => SetProperty(ref _rows, value);
    }

    public CommitRow? Selected
    {
        get => _selected;
        private set => SetProperty(ref _selected, value);
    }

    public CommitDetails? Details
    {
        get => _details;
        private set => SetProperty(ref _details, value);
    }

    public string DetailsError
    {
        get => _detailsError ?? "";
        private set => SetProperty(ref _detailsError, value);
    }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public string RepositoryName
    {
        get => _repositoryName;
        private set => SetProperty(ref _repositoryName, value);
    }

    public bool HasMore
    {
        get => _hasMore;
        private set => SetProperty(ref _hasMore, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    /// <summary>
    ///  Set when a repository cannot be read. A view shows it and then calls <see cref="ClearError"/>.
    /// </summary>
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public void ClearError() => ErrorMessage = null;

    public Task OpenAsync(string repositoryPath) => LoadPagesAsync(repositoryPath, pages: 1);

    public Task LoadMoreAsync()
        => _repositoryPath is null ? Task.CompletedTask : LoadPagesAsync(_repositoryPath, _pages + 1);

    public async Task SelectAsync(CommitRow? row)
    {
        Selected = row;
        int version = ++_detailsVersion;
        string? repositoryPath = _repositoryPath;

        if (row is null || repositoryPath is null)
        {
            Details = null;
            DetailsError = "";
            return;
        }

        try
        {
            CommitDetails details = await _history.LoadDetailsAsync(repositoryPath, row.Hash);
            if (version == _detailsVersion)
            {
                DetailsError = "";
                Details = details;
            }
        }
        catch (Exception ex)
        {
            if (version == _detailsVersion)
            {
                Details = null;
                DetailsError = ex.Message;
            }
        }
    }

    private async Task LoadPagesAsync(string repositoryPath, int pages)
    {
        int version = ++_loadVersion;
        IsLoading = true;
        Status = "Loading...";
        ClearSelection();

        try
        {
            CommitPage page = await _history.LoadPageAsync(repositoryPath, pages * PageSize);
            if (version != _loadVersion)
            {
                return;
            }

            _repositoryPath = repositoryPath;
            _pages = pages;
            RepositoryName = Path.GetFileName(repositoryPath.TrimEnd('/', '\\'));
            Rows = page.Rows;
            HasMore = page.HasMore;
            string count = page.Rows.Count == 1 ? "1 commit" : $"{page.Rows.Count} commits";
            Status = page.HasMore ? $"{count}, more available" : count;
        }
        catch (Exception ex)
        {
            if (version != _loadVersion)
            {
                return;
            }

            _repositoryPath = null;
            _pages = 0;
            Rows = [];
            HasMore = false;
            Status = "";
            RepositoryName = "";
            ErrorMessage = ex.Message;
        }
        finally
        {
            if (version == _loadVersion)
            {
                IsLoading = false;
            }
        }
    }

    private void ClearSelection()
    {
        _detailsVersion++;
        Selected = null;
        Details = null;
        DetailsError = "";
    }
}
