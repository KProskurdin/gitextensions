namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  Runs user-initiated operations and reports their outcome. Holds no UI types. Create and use it on the UI thread.
///  Every operation that changes the repository raises <see cref="RepositoryChanged"/> so a view can reload it.
/// </summary>
public sealed class RepositoryOperationsViewModel : ObservableObject
{
    private readonly IGitOperations _operations;
    private bool _isBusy;
    private string _statusMessage = "";
    private string? _errorMessage;

    public RepositoryOperationsViewModel(IGitOperations operations)
    {
        _operations = operations;
    }

    public event EventHandler<RepositoryChangedEventArgs>? RepositoryChanged;

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    /// <summary>
    ///  Describes the last successful operation, e.g. "Committed".
    /// </summary>
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>
    ///  Set when an operation fails or its input is invalid. A view shows it and then calls <see cref="ClearError"/>.
    /// </summary>
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public void ClearError() => ErrorMessage = null;

    public Task<bool> StageAsync(string repositoryPath, IReadOnlyList<string> paths)
        => RunAsync("Staged", repositoryPath, () => _operations.StageAsync(repositoryPath, paths));

    public Task<bool> UnstageAsync(string repositoryPath, IReadOnlyList<string> paths)
        => RunAsync("Unstaged", repositoryPath, () => _operations.UnstageAsync(repositoryPath, paths));

    public Task<bool> CommitAsync(string repositoryPath, string message, bool amend)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return RejectAsync("Enter a commit message.");
        }

        return RunAsync(amend ? "Amended" : "Committed", repositoryPath,
            () => _operations.CommitAsync(repositoryPath, message, amend));
    }

    public Task<bool> CreateBranchAsync(string repositoryPath, string name, bool checkout)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return RejectAsync("Enter a branch name.");
        }

        return RunAsync($"Created {name.Trim()}", repositoryPath,
            () => _operations.CreateBranchAsync(repositoryPath, name.Trim(), checkout));
    }

    public Task<bool> CheckoutAsync(string repositoryPath, string branch)
        => RunAsync($"Checked out {branch}", repositoryPath, () => _operations.CheckoutAsync(repositoryPath, branch));

    /// <summary>
    ///  Checks out <paramref name="remoteBranch"/> as a local tracking branch with the same name after the remote.
    /// </summary>
    public Task<bool> CheckoutRemoteAsync(string repositoryPath, string remoteBranch)
        => RunAsync($"Checked out {remoteBranch}", repositoryPath,
            () => _operations.CheckoutRemoteAsync(repositoryPath, remoteBranch));

    public Task<bool> DeleteBranchAsync(string repositoryPath, string branch, bool force)
        => RunAsync($"Deleted {branch}", repositoryPath,
            () => _operations.DeleteBranchAsync(repositoryPath, branch, force));

    public Task<bool> FetchAsync(string repositoryPath, string remote)
        => RunAsync("Fetched", repositoryPath, () => _operations.FetchAsync(repositoryPath, remote));

    public Task<bool> PullAsync(string repositoryPath, string remote, string branch, bool rebase)
        => RunAsync("Pulled", repositoryPath, () => _operations.PullAsync(repositoryPath, remote, branch, rebase));

    public Task<bool> PushAsync(string repositoryPath, string remote, string branch)
        => RunAsync("Pushed", repositoryPath, () => _operations.PushAsync(repositoryPath, remote, branch));

    /// <summary>
    ///  Clones <paramref name="sourceUrl"/> into <paramref name="targetPath"/>. The clone is reported through
    ///  <see cref="RepositoryChanged"/> with the target path, so a view can open it.
    /// </summary>
    public Task<bool> CloneAsync(string sourceUrl, string targetPath)
    {
        if (string.IsNullOrWhiteSpace(sourceUrl))
        {
            return RejectAsync("Enter a repository URL or path to clone.");
        }

        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return RejectAsync("Enter a folder to clone into.");
        }

        return RunAsync("Cloned", targetPath, () => _operations.CloneAsync(sourceUrl.Trim(), targetPath.Trim()));
    }

    private Task<bool> RejectAsync(string message)
    {
        ErrorMessage = null;
        ErrorMessage = message;
        return Task.FromResult(false);
    }

    private async Task<bool> RunAsync(string successMessage, string repositoryPath, Func<Task> operation)
    {
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            await operation();
            StatusMessage = successMessage;
            RepositoryChanged?.Invoke(this, new RepositoryChangedEventArgs(repositoryPath));
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = "";
            ErrorMessage = null;
            ErrorMessage = ex.Message;
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }
}

public sealed class RepositoryChangedEventArgs(string repositoryPath) : EventArgs
{
    public string RepositoryPath { get; } = repositoryPath;
}
