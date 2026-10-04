using GitExtensions.Xplat.Core.CommitHistory;

namespace GitExtensions.Xplat.Core.Tests;

/// <summary>
///  Each read stays pending until the test completes it, so the test decides the order in which reads finish.
/// </summary>
internal sealed class FakeCommitHistory : ICommitHistory
{
    private readonly List<TaskCompletionSource<CommitPage>> _pageReads = [];
    private readonly List<TaskCompletionSource<CommitDetails>> _detailReads = [];

    public List<int> PageLimits { get; } = [];

    public List<string> DetailHashes { get; } = [];

    public Task<CommitPage> LoadPageAsync(string repositoryPath, int limit)
    {
        PageLimits.Add(limit);
        TaskCompletionSource<CommitPage> read = new();
        _pageReads.Add(read);
        return read.Task;
    }

    public Task<CommitDetails> LoadDetailsAsync(string repositoryPath, string hash)
    {
        DetailHashes.Add(hash);
        TaskCompletionSource<CommitDetails> read = new();
        _detailReads.Add(read);
        return read.Task;
    }

    public void CompletePage(int index, CommitPage page) => _pageReads[index].SetResult(page);

    public void FailPage(int index, Exception exception) => _pageReads[index].SetException(exception);

    public void CompleteDetails(int index, CommitDetails details) => _detailReads[index].SetResult(details);

    public void FailDetails(int index, Exception exception) => _detailReads[index].SetException(exception);

    public IReadOnlyList<CommitFile> Files { get; set; } = [];

    public IReadOnlyList<string> Tree { get; set; } = [];

    public List<(string Hash, string FilePath)> FileHistoryRequests { get; } = [];

    public IReadOnlyList<CommitRow> FileHistory { get; set; } = [];

    public Task<IReadOnlyList<string>> LoadTreeAsync(string repositoryPath, string hash) => Task.FromResult(Tree);

    public Task<CommitPage> LoadFileHistoryAsync(string repositoryPath, string hash, string filePath, int limit)
    {
        FileHistoryRequests.Add((hash, filePath));
        return Task.FromResult(new CommitPage(FileHistory, HasMore: false));
    }

    public IReadOnlyList<BlameLine> Blame { get; set; } = [];

    public Task<IReadOnlyList<BlameLine>> LoadBlameAsync(string repositoryPath, string hash, string filePath) => Task.FromResult(Blame);

    public Task<IReadOnlyList<CommitFile>> LoadFilesAsync(string repositoryPath, string hash) => Task.FromResult(Files);
}
