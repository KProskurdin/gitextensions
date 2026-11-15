using CommunityToolkit.Mvvm.ComponentModel;

namespace GitExtensions.Xplat.Core.CommitHistory;

/// <summary>
///  Upstream's <c>FormBlame</c> (PLAN.md principle 6): a file's blame as of a commit in its own window, with the blame's
///  errors above it; the blame is this one's (<see cref="Blame"/>).
/// </summary>
public sealed partial class BlameWindowViewModel : ObservableObject
{
    private readonly string _repositoryPath;
    private readonly string _hash;
    private readonly string _filePath;

    public BlameWindowViewModel(BlameViewModel blame, string repositoryPath, string hash, string filePath)
    {
        Blame = blame;
        _repositoryPath = repositoryPath;
        _hash = hash;
        _filePath = filePath;
        Title = $"Blame: {filePath}";
        blame.ErrorOccurred += (_, message) => ErrorMessage = message;
    }

    public BlameViewModel Blame { get; }

    public string Title { get; }

    /// <summary>
    ///  The last error of the blame, shown above it.
    /// </summary>
    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    /// <summary>
    ///  Blames the file; the window calls it when it opens.
    /// </summary>
    public async Task LoadAsync()
    {
        try
        {
            await Blame.LoadAsync(_repositoryPath, _hash, _filePath);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ErrorMessage = ex.Message;
        }
    }
}
