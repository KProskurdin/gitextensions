using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  Runs write and remote operations with the shared git engine. Commands are built by the upstream builders, so the
///  arguments match what the WinForms app runs.
/// </summary>
public sealed class GitOperations : IGitOperations
{
    public Task StageAsync(string repositoryPath, IReadOnlyList<string> paths)
        => RunAsync(repositoryPath, _ =>
        {
            GitArgumentBuilder arguments = new("add") { "--" };
            foreach (string path in paths)
            {
                arguments.Add(path.Quote());
            }

            return arguments;
        });

    public Task UnstageAsync(string repositoryPath, IReadOnlyList<string> paths)
        => RunAsync(repositoryPath, _ =>
        {
            GitArgumentBuilder arguments = new("reset") { "-q", "--" };
            foreach (string path in paths)
            {
                arguments.Add(path.Quote());
            }

            return arguments;
        });

    public Task CommitAsync(string repositoryPath, string message, bool amend)
        => Task.Run(() =>
        {
            GitModule module = CreateModule(repositoryPath);
            string messageFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(messageFile, message);
                ArgumentString commit = Commands.Commit(amend, signOff: false, author: "",
                    useExplicitCommitMessage: true,
                    commitMessageFile: messageFile, getPathForGitExecution: module.GetPathForGitExecution);
                Execute(module.GitExecutable, commit);
            }
            finally
            {
                File.Delete(messageFile);
            }
        });

    public Task CreateBranchAsync(string repositoryPath, string name, bool checkout)
        => Task.Run(() =>
        {
            GitModule module = CreateModule(repositoryPath);
            Execute(module.GitExecutable, Commands.Branch(name, module.GetCurrentCheckout(), checkout));
        });

    public Task CheckoutRemoteAsync(string repositoryPath, string remoteBranch)
        => RunAsync(repositoryPath, _ => new GitArgumentBuilder("checkout") { "--track", remoteBranch.Quote() });

    public Task CheckoutAsync(string repositoryPath, string branch)
        => RunAsync(repositoryPath, _ => Commands.Checkout(branch, LocalChangesAction.DontChange));

    public Task DeleteBranchAsync(string repositoryPath, string branch, bool force)
        => RunAsync(repositoryPath, _ => new GitArgumentBuilder("branch") { force ? "-D" : "-d", branch.Quote() });

    public Task FetchAsync(string repositoryPath, string remote)
        => Task.Run(() =>
        {
            GitModule module = CreateModule(repositoryPath);
            Execute(module.GitExecutable, module.FetchCmd(remote, remoteBranch: "", localBranch: ""));
        });

    public Task PullAsync(string repositoryPath, string remote, string branch, bool rebase)
        => Task.Run(() =>
        {
            GitModule module = CreateModule(repositoryPath);
            Execute(module.GitExecutable, module.PullCmd(remote, branch, rebase, fetchTags: false));
        });

    public Task PushAsync(string repositoryPath, string remote, string branch)
        => RunAsync(repositoryPath,
            _ => Commands.Push(remote, branch, toBranch: null, ForcePushOptions.DoNotForce, track: true,
                recursiveSubmodules: 0));

    public Task MergeAsync(string repositoryPath, string branch)
        => Task.Run(() =>
        {
            GitModule module = CreateModule(repositoryPath);
            Execute(module.GitExecutable, Commands.MergeBranch(branch, allowFastForward: true, squash: false, noCommit: false, strategy: "",
                allowUnrelatedHistories: false, mergeCommitFilePath: null, module.GetPathForGitExecution, log: null));
        });

    public Task AbortMergeAsync(string repositoryPath)
        => RunAsync(repositoryPath, _ => Commands.AbortMerge());

    public Task StashAsync(string repositoryPath, string message)
        => RunAsync(repositoryPath, _ => Commands.StashSave(untracked: false, keepIndex: false, message, selectedFiles: null));

    public Task PopStashAsync(string repositoryPath)
        => RunAsync(repositoryPath, _ => new GitArgumentBuilder("stash") { "pop" });

    public Task CloneAsync(string sourceUrl, string targetPath)
        => Task.Run(() =>
        {
            string fullTarget = Path.GetFullPath(targetPath);
            string parent = Path.GetDirectoryName(fullTarget)
                            ?? throw new InvalidOperationException($"Cannot clone into {targetPath}");
            Directory.CreateDirectory(parent);

            string source = PathUtil.IsLocalFile(sourceUrl) ? sourceUrl.ToPosixPath() : sourceUrl;
            ArgumentString clone = Commands.Clone(source, fullTarget, path => path?.ToPosixPath());
            Execute(new Executable(AppSettings.GitCommand, parent), clone);
        });

    private static Task RunAsync(string repositoryPath, Func<GitModule, ArgumentString> build)
        => Task.Run(() =>
        {
            GitModule module = CreateModule(repositoryPath);
            Execute(module.GitExecutable, build(module));
        });

    private static void Execute(IExecutable executable, ArgumentString arguments)
    {
        ExecutionResult result = executable.Execute(arguments, throwOnErrorExit: false);
        if (result.ExitedSuccessfully)
        {
            return;
        }

        string detail = result.StandardError.Trim();
        throw new GitOperationException(detail.Length > 0
            ? detail
            : $"git {arguments} failed with exit code {result.ExitCode}");
    }

    private static GitModule CreateModule(string path)
        => new(new GitExecutorProvider(new GitDirectoryResolver()), path);
}
