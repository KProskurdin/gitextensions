namespace GitExtensions.Xplat.Core.Diff;

/// <summary>
///  The diff of one file, for a view to show. Holds no UI types.
/// </summary>
public sealed class DiffViewModel : ObservableObject
{
    private readonly IDiffService _diff;
    private int _loadVersion;
    private IReadOnlyList<DiffLine> _lines = [];
    private bool _isLoading;
    private string? _errorMessage;

    public DiffViewModel(IDiffService diff)
    {
        _diff = diff;
    }

    public IReadOnlyList<DiffLine> Lines
    {
        get => _lines;
        private set => SetProperty(ref _lines, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    /// <summary>
    ///  Set when the diff cannot be read. A view shows it and then calls <see cref="ClearError"/>.
    /// </summary>
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public void ClearError() => ErrorMessage = null;

    public async Task LoadAsync(string repositoryPath, string? commitHash, string? filePath, bool staged,
        DiffOptions? options = null)
    {
        int version = ++_loadVersion;
        IsLoading = true;
        try
        {
            IReadOnlyList<DiffLine> lines = await _diff.GetDiffAsync(repositoryPath, commitHash, filePath, staged, options);
            if (version == _loadVersion)
            {
                Lines = lines;
            }
        }
        catch (Exception ex)
        {
            if (version == _loadVersion)
            {
                Lines = [];
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
}
