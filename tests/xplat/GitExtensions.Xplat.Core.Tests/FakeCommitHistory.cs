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
}
