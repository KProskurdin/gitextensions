using GitExtensions.Xplat.Core.Operations;

namespace GitExtensions.Xplat.Core.Tests;

/// <summary>
///  Records each call and keeps it pending until the test completes or fails it.
/// </summary>
internal sealed class FakeGitOperations : IGitOperations
{
    private readonly List<(string Name, string Arguments, TaskCompletionSource Completion)> _calls = [];

    public int Count => _calls.Count;

    public string NameAt(int index) => _calls[index].Name;

    public string ArgumentsAt(int index) => _calls[index].Arguments;

    public Task StageAsync(string repositoryPath, IReadOnlyList<string> paths) => Record("Stage", $"{repositoryPath} {string.Join(",", paths)}");

    public Task UnstageAsync(string repositoryPath, IReadOnlyList<string> paths) => Record("Unstage", $"{repositoryPath} {string.Join(",", paths)}");

    public Task CommitAsync(string repositoryPath, string message, bool amend) => Record("Commit", $"{repositoryPath} {message} amend={amend}");

    public Task CreateBranchAsync(string repositoryPath, string name, bool checkout) => Record("CreateBranch", $"{repositoryPath} {name} checkout={checkout}");

    public Task CheckoutAsync(string repositoryPath, string branch) => Record("Checkout", $"{repositoryPath} {branch}");

    public Task DeleteBranchAsync(string repositoryPath, string branch, bool force) => Record("DeleteBranch", $"{repositoryPath} {branch} force={force}");

    public Task FetchAsync(string repositoryPath, string remote) => Record("Fetch", $"{repositoryPath} {remote}");

    public Task PullAsync(string repositoryPath, string remote, string branch, bool rebase) => Record("Pull", $"{repositoryPath} {remote} {branch} rebase={rebase}");

    public Task PushAsync(string repositoryPath, string remote, string branch) => Record("Push", $"{repositoryPath} {remote} {branch}");

    public Task CloneAsync(string sourceUrl, string targetPath) => Record("Clone", $"{sourceUrl} {targetPath}");

    public void Complete(int index) => _calls[index].Completion.SetResult();

    public void Fail(int index, Exception exception) => _calls[index].Completion.SetException(exception);

    private Task Record(string name, string arguments)
    {
        TaskCompletionSource completion = new();
        _calls.Add((name, arguments, completion));
        return completion.Task;
    }
}
