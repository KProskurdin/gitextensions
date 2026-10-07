using System.IO;

namespace GitExtensions.Xplat.Core.CommitHistory;

/// <summary>
///  A commit as the list shows it: its row and where it sits in the revision graph.
/// </summary>
public sealed record CommitListItem(CommitRow Row, GraphRowRef Graph)
{
    /// <summary>
    ///  The branch and tag names of the commit in brackets, e.g. "[main] [v1.0] ", or empty when none point at it.
    /// </summary>
    public string RefsText =>
        Row.Refs is { Count: > 0 } refs ? string.Join(" ", refs.Select(name => $"[{name}]")) + " " : "";

    /// <summary>
    ///  The labels drawn before the subject. Rows read without kinds show their names as branches.
    /// </summary>
    public IReadOnlyList<RefLabel> Labels =>
        Row.Labels ?? [.. (Row.Refs ?? []).Select(name => new RefLabel(name, RefKind.Branch))];
}

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
    private CommitGraph? _graph;
    private IReadOnlyList<CommitRow>? _graphRows;
    private IReadOnlyList<CommitListItem> _visibleRows = [];
    private string _filterText = "";
    private RevisionFilter _filter = RevisionFilter.AllBranches;
    private CommitRow? _selected;
    private CommitDetails? _details;
    private string _status = "";
    private string _repositoryName = "";
    private bool _hasMore;
    private bool _isLoading;
    private string? _errorMessage;
    private string? _detailsError;
    private IReadOnlyList<CommitFile> _commitFiles = [];

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
        private set
        {
            if (SetProperty(ref _rows, value))
            {
                RefreshVisibleRows();
            }
        }
    }

    /// <summary>
    ///  The loaded commits that match <see cref="FilterText"/>; all of them when the filter is empty.
    /// </summary>
    public IReadOnlyList<CommitListItem> VisibleRows
    {
        get => _visibleRows;
        private set => SetProperty(ref _visibleRows, value);
    }

    /// <summary>
    ///  Matches commits whose hash, subject or author contains the text, ignoring case. Only loaded commits are filtered.
    /// </summary>
    public string FilterText
    {
        get => _filterText;
        set
        {
            if (SetProperty(ref _filterText, value.Trim()))
            {
                RefreshVisibleRows();
            }
        }
    }

    /// <summary>
    ///  Which commits are read: the branch choice and the advanced filter. Change it with <see cref="ApplyFilterAsync"/>.
    /// </summary>
    public RevisionFilter Filter
    {
        get => _filter;
        private set => SetProperty(ref _filter, value);
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

    /// <summary>
    ///  Files changed by the selected commit.
    /// </summary>
    public IReadOnlyList<CommitFile> CommitFiles
    {
        get => _commitFiles;
        private set => SetProperty(ref _commitFiles, value);
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

    // The list shows the commits in the graph's order, as upstream's grid does. The graph is built once per read; the quick
    // filter only hides rows, which keep their place in the graph.
    private void RefreshVisibleRows()
    {
        if (_graph is null || !ReferenceEquals(_graphRows, _rows))
        {
            _graph = CommitGraph.Build(_rows);
            _graphRows = _rows;
        }

        List<CommitListItem> items = [];
        for (int i = 0; i < _graph.OrderedRows.Count; i++)
        {
            CommitRow row = _graph.OrderedRows[i];
            if (_filterText.Length == 0 || Matches(row, _filterText))
            {
                items.Add(new CommitListItem(row, _graph.RowAt(i)));
            }
        }

        VisibleRows = items;
    }

    private static bool Matches(CommitRow row, string text) =>
        row.Hash.Contains(text, StringComparison.OrdinalIgnoreCase)
        || row.Subject.Contains(text, StringComparison.OrdinalIgnoreCase)
        || row.Author.Contains(text, StringComparison.OrdinalIgnoreCase);

    public Task OpenAsync(string repositoryPath) => LoadPagesAsync(repositoryPath, pages: 1);

    /// <summary>
    ///  Reads the first page again with <paramref name="filter"/>. Before a repository is open it only sets the filter for
    ///  the next open.
    /// </summary>
    public Task ApplyFilterAsync(RevisionFilter filter)
    {
        Filter = filter;
        return _repositoryPath is null ? Task.CompletedTask : LoadPagesAsync(_repositoryPath, pages: 1);
    }

    /// <summary>
    ///  Closes the repository: the list empties and <see cref="RepositoryPath"/> becomes null. A read still in flight is
    ///  ignored when it completes.
    /// </summary>
    public void Close()
    {
        _loadVersion++;
        ClearSelection();
        _repositoryPath = null;
        _pages = 0;
        CommitFiles = [];
        Rows = [];
        HasMore = false;
        Status = "";
        RepositoryName = "";
        IsLoading = false;
    }

    public Task LoadMoreAsync()
        => _repositoryPath is null ? Task.CompletedTask : LoadPagesAsync(_repositoryPath, _pages + 1);

    /// <summary>
    ///  Loads enough pages to hold at least <paramref name="count"/> commits, in one read.
    /// </summary>
    public Task LoadAtLeastAsync(int count)
        => _repositoryPath is null
            ? Task.CompletedTask
            : LoadPagesAsync(_repositoryPath, Math.Max(1, (count + PageSize - 1) / PageSize));

    /// <summary>
    ///  The shown row of <paramref name="row"/>'s first parent (upstream's grid "go to parent"), or null when it is not
    ///  loaded or filtered out.
    /// </summary>
    public int? IndexOfParent(CommitRow row)
        => row.ParentHashes is [{ } parent, ..] ? IndexOf(item => item.Row.Hash == parent) : null;

    /// <summary>
    ///  The nearest shown row above <paramref name="row"/> that has it as a parent (upstream's "go to child"), or null.
    /// </summary>
    public int? IndexOfChild(CommitRow row)
    {
        int index = IndexOf(item => item.Row.Hash == row.Hash) ?? 0;
        for (int i = index - 1; i >= 0; i--)
        {
            if (_visibleRows[i].Row.ParentHashes?.Contains(row.Hash) == true)
            {
                return i;
            }
        }

        return null;
    }

    /// <summary>
    ///  The shown row labeled HEAD (upstream's "select current revision"), or null.
    /// </summary>
    public int? IndexOfHead()
        => IndexOf(item => item.Row.Labels?.Any(label => label.Kind == RefKind.Head) == true);

    private int? IndexOf(Func<CommitListItem, bool> match)
    {
        for (int i = 0; i < _visibleRows.Count; i++)
        {
            if (match(_visibleRows[i]))
            {
                return i;
            }
        }

        return null;
    }

    public async Task SelectAsync(CommitRow? row)
    {
        Selected = row;
        int version = ++_detailsVersion;
        string? repositoryPath = _repositoryPath;

        if (row is null || repositoryPath is null)
        {
            Details = null;
            CommitFiles = [];
            DetailsError = "";
            return;
        }

        try
        {
            CommitDetails details = await _history.LoadDetailsAsync(repositoryPath, row.Hash);
            IReadOnlyList<CommitFile> files = await _history.LoadFilesAsync(repositoryPath, row.Hash);
            if (version == _detailsVersion)
            {
                DetailsError = "";
                Details = details;
                CommitFiles = files;
            }
        }
        catch (Exception ex)
        {
            if (version == _detailsVersion)
            {
                Details = null;
                CommitFiles = [];
                DetailsError = ex.Message;
            }
        }
    }

    /// <summary>
    ///  Shows the commits whose message contains <paramref name="text"/>. Paging does not apply to a search, and empty text
    ///  shows the first page of HEAD again.
    /// </summary>
    public async Task SearchAsync(string text)
    {
        if (_repositoryPath is null)
        {
            return;
        }

        string search = text.Trim();
        if (search.Length == 0)
        {
            await LoadPagesAsync(_repositoryPath, 1);
            return;
        }

        int version = ++_loadVersion;
        IsLoading = true;
        Status = "Searching...";
        ClearSelection();

        try
        {
            CommitPage page = await _history.SearchAsync(_repositoryPath, search, PageSize);
            if (version != _loadVersion)
            {
                return;
            }

            if (!await PrepareGraphAsync(page.Rows, version))
            {
                return;
            }

            Rows = page.Rows;
            HasMore = false;
            string count = page.Rows.Count == 1 ? "1 commit" : $"{page.Rows.Count} commits";
            Status = page.HasMore ? $"{count} match, more not shown" : $"{count} match";
        }
        catch (Exception ex)
        {
            if (version == _loadVersion)
            {
                Rows = [];
                HasMore = false;
                Status = "";
                ErrorMessage = ex.Message;
            }
        }
        finally
        {
            if (version == _loadVersion)
            {
                IsLoading = false;
            }
        }
    }

    // Upstream's graph straightens its lanes while it is built, which takes a while for thousands of commits, so it is built
    // off the UI thread before the rows are shown. False when a newer read has started meanwhile.
    private async Task<bool> PrepareGraphAsync(IReadOnlyList<CommitRow> rows, int version)
    {
        CommitGraph graph = await Task.Run(() => CommitGraph.Build(rows));
        if (version != _loadVersion)
        {
            return false;
        }

        _graph = graph;
        _graphRows = rows;
        return true;
    }

    private async Task LoadPagesAsync(string repositoryPath, int pages)
    {
        int version = ++_loadVersion;
        IsLoading = true;
        Status = "Loading...";
        ClearSelection();

        try
        {
            CommitPage page = await _history.LoadPageAsync(repositoryPath, pages * PageSize, _filter);
            if (version != _loadVersion)
            {
                return;
            }

            if (!await PrepareGraphAsync(page.Rows, version))
            {
                return;
            }

            _repositoryPath = repositoryPath;
            _pages = pages;
            RepositoryName = Path.GetFileName(repositoryPath.TrimEnd('/', '\\'));
            Rows = page.Rows;
            HasMore = page.HasMore;
            string count = page.Rows.Count == 1 ? "1 commit" : $"{page.Rows.Count} commits";
            string filtered = _filter.IsNarrowed ? " (filtered)" : "";
            Status = page.HasMore ? $"{count}{filtered}, more available" : count + filtered;
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
