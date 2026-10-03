using GitExtensions.Xplat.Core.Repository;

namespace GitExtensions.Xplat.Core.Tests;

/// <summary>
///  Each read stays pending until the test completes it, so the test decides the order in which reads finish.
/// </summary>
internal sealed class FakeRepositoryService : IRepositoryService
{
    private readonly List<TaskCompletionSource<RepositorySnapshot>> _reads = [];

    public Task<RepositorySnapshot> GetSnapshotAsync(string repositoryPath)
    {
        TaskCompletionSource<RepositorySnapshot> read = new();
        _reads.Add(read);
        return read.Task;
    }

    public void Complete(int index, RepositorySnapshot snapshot) => _reads[index].SetResult(snapshot);

    public void Fail(int index, Exception exception) => _reads[index].SetException(exception);
}
