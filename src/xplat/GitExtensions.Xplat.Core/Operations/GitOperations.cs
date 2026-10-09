using System.Text;
using GitCommands;
using GitCommands.Git;
using GitCommands.Patches;
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
    private readonly IReadOnlyDictionary<string, string>? _remoteEnvironment;

    /// <summary>
    ///  <paramref name="askPassExecutable"/> is the app that answers ssh's and git's prompts during remote operations
    ///  (see <see cref="GitAskPass"/>); without it git relies on its credential helpers alone.
    /// </summary>
    public GitOperations(string? askPassExecutable = null)
    {
        _remoteEnvironment = askPassExecutable is null ? null : GitAskPass.Environment(askPassExecutable);
    }

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

    // Upstream's FileViewer builds the patch with PatchManager and applies it to the index the same way.
    // As upstream's FileViewer.StageSelectedLines. Upstream stages lines of an untracked file from the file's text
    // (GetSelectedLinesAsNewPatch); the new shell shows that file as a diff from /dev/null, whose "new file" header already
    // makes the patch add the file. For unstaging, isNewFile turns the header into a change of the staged file.
    public Task StageLinesAsync(string repositoryPath, string diffText, int selectionStart, int selectionLength,
        bool unstage, bool isNewFile = false)
        => Task.Run(() =>
        {
            byte[]? patch = PatchManager.GetSelectedLinesAsPatch(diffText, selectionStart, selectionLength,
                isIndex: unstage,
                Encoding.UTF8, reset: false, isNewFile: unstage && isNewFile, isRenamed: false);
            if (patch is not { Length: > 0 })
            {
                throw new GitOperationException("The selected lines have no added or removed line to apply.");
            }

            GitModule module = CreateModule(repositoryPath);
            ExecutionResult result = module.GitExecutable.Execute(
                new GitArgumentBuilder("apply")
                {
                    "--cached", "--index", "--whitespace=nowarn", { unstage, "--reverse" }
                },
                input => input.BaseStream.Write(patch),
                throwOnErrorExit: false);
            if (!result.ExitedSuccessfully)
            {
                string detail = result.AllOutput.Trim();
                throw new GitOperationException(detail.Length > 0
                    ? detail
                    : $"git apply failed with exit code {result.ExitCode}");
            }
        });

    public Task CommitAsync(string repositoryPath, string message, bool amend, bool signOff, string author)
        => Task.Run(() =>
        {
            GitModule module = CreateModule(repositoryPath);
            string messageFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(messageFile, message);
                ArgumentString commit = Commands.Commit(amend, signOff, author,
                    useExplicitCommitMessage: true,
                    commitMessageFile: messageFile, getPathForGitExecution: module.GetPathForGitExecution);
                Execute(module.GitExecutable, commit);
            }
            finally
            {
                File.Delete(messageFile);
            }
        });

    public Task CreateBranchAsync(string repositoryPath, string name, bool checkout, string? startPoint = null)
        => Task.Run(() =>
        {
            GitModule module = CreateModule(repositoryPath);
            ArgumentString create = startPoint is null
                ? Commands.Branch(name, module.GetCurrentCheckout(), checkout)
                : new GitArgumentBuilder(checkout ? "checkout" : "branch")
                {
                    { checkout, "-b" }, name.Trim().Quote(), startPoint.Quote()
                };
            Execute(module.GitExecutable, create);
        });

    public Task CheckoutRemoteAsync(string repositoryPath, string remoteBranch)
        => RunAsync(repositoryPath, _ => new GitArgumentBuilder("checkout") { "--track", remoteBranch.Quote() });

    // Stash is not a git checkout option: like upstream's checkout dialog, the changes are stashed, the branch checked out,
    // and the stash popped (a conflicting pop leaves the stash in place and reports git's message).
    public Task CheckoutAsync(string repositoryPath, string branch,
        LocalChangesAction localChanges = LocalChangesAction.DontChange)
        => Task.Run(() =>
        {
            GitModule module = CreateModule(repositoryPath);
            if (localChanges != LocalChangesAction.Stash)
            {
                Execute(module.GitExecutable, Commands.Checkout(branch, localChanges));
                return;
            }

            Execute(module.GitExecutable,
                Commands.StashSave(AppSettings.IncludeUntrackedFilesInAutoStash, keepIndex: false,
                    message: $"Checkout of {branch}", selectedFiles: null));
            Execute(module.GitExecutable, Commands.Checkout(branch, LocalChangesAction.DontChange));
            Execute(module.GitExecutable, new GitArgumentBuilder("stash") { "pop", "-q" });
        });

    public Task DeleteBranchAsync(string repositoryPath, string branch, bool force)
        => RunAsync(repositoryPath, _ => new GitArgumentBuilder("branch") { force ? "-D" : "-d", branch.Quote() });

    public Task DeleteRemoteBranchAsync(string repositoryPath, string remote, string branch)
        => RunAsync(repositoryPath, _ => new GitArgumentBuilder("push") { remote.Quote(), "--delete", branch.Quote() });

    public Task RenameBranchAsync(string repositoryPath, string branch, string newName)
        => RunAsync(repositoryPath, _ => Commands.RenameBranch(branch, newName));

    public Task FetchAsync(string repositoryPath, string remote, bool prune, IProgress<GitOutputLine>? output = null,
        CancellationToken cancellationToken = default)
        => RunRemoteAsync(repositoryPath,
            module => module.FetchCmd(remote, remoteBranch: "", localBranch: "", pruneRemoteBranches: prune), output,
            cancellationToken);

    public Task PullAsync(string repositoryPath, string remote, string branch, bool rebase,
        IProgress<GitOutputLine>? output = null, CancellationToken cancellationToken = default)
        => RunRemoteAsync(repositoryPath, module => module.PullCmd(remote, branch, rebase, fetchTags: false), output,
            cancellationToken);

    public Task PushAsync(string repositoryPath, string remote, string branch, IProgress<GitOutputLine>? output = null,
        CancellationToken cancellationToken = default)
        => RunRemoteAsync(repositoryPath,
            _ => Commands.Push(remote, branch, toBranch: null, ForcePushOptions.DoNotForce, track: true,
                recursiveSubmodules: 0),
            output, cancellationToken);

    public Task PushAsync(string repositoryPath, PushRequest request, IProgress<GitOutputLine>? output = null,
        CancellationToken cancellationToken = default)
        => RunRemoteAsync(repositoryPath,
            _ => Commands.Push(request.Remote, request.LocalBranch,
                request.RemoteBranch.Length == 0 ? null : request.RemoteBranch, request.Force,
                request.Track, recursiveSubmodules: 0),
            output, cancellationToken);

    // Upstream's PullCmd has no autostash option; git's own settings, given for this command only, stash and restore the
    // local changes around the merge or rebase.
    public Task PullAsync(string repositoryPath, PullRequest request, IProgress<GitOutputLine>? output = null,
        CancellationToken cancellationToken = default)
        => RunRemoteAsync(repositoryPath, module =>
        {
            if (request.Action == PullAction.FetchOnly)
            {
                return module.FetchCmd(request.Remote, request.RemoteBranch, localBranch: "",
                    pruneRemoteBranches: request.Prune);
            }

            ArgumentString pull = module.PullCmd(request.Remote, request.RemoteBranch,
                request.Action == PullAction.Rebase, fetchTags: false);
            return request.AutoStash
                ? (ArgumentString)$"-c rebase.autoStash=true -c merge.autoStash=true {pull}"
                : pull;
        }, output, cancellationToken);

    public Task PushTagsAsync(string repositoryPath, string remote, IProgress<GitOutputLine>? output = null,
        CancellationToken cancellationToken = default)
        => RunRemoteAsync(repositoryPath, _ => Commands.PushTag(remote, tag: "", all: true), output, cancellationToken);

    public Task MergeAsync(string repositoryPath, string branch)
        => Task.Run(() =>
        {
            GitModule module = CreateModule(repositoryPath);
            Execute(module.GitExecutable, Commands.MergeBranch(branch, allowFastForward: true, squash: false,
                noCommit: false, strategy: "",
                allowUnrelatedHistories: false, mergeCommitFilePath: null, module.GetPathForGitExecution, log: null));
        });

    public Task AbortMergeAsync(string repositoryPath)
        => RunAsync(repositoryPath, _ => Commands.AbortMerge());

    public Task DeleteUntrackedAsync(string repositoryPath, IReadOnlyList<string> paths)
        => RunAsync(repositoryPath, _ =>
        {
            GitArgumentBuilder clean = new("clean") { "-f", "--" };
            foreach (string path in paths)
            {
                clean.Add(path.Quote());
            }

            return clean;
        });

    public Task AddRemoteAsync(string repositoryPath, string name, string url)
        => RunAsync(repositoryPath, _ => new GitArgumentBuilder("remote") { "add", name.Quote(), url.Quote() });

    public Task RemoveRemoteAsync(string repositoryPath, string name)
        => RunAsync(repositoryPath, _ => new GitArgumentBuilder("remote") { "remove", name.Quote() });

    // Same arguments as upstream's GitModule.RenameRemote, with git's error reported.
    public Task RenameRemoteAsync(string repositoryPath, string name, string newName)
        => RunAsync(repositoryPath, _ => new GitArgumentBuilder("remote") { "rename", name.Quote(), newName.Quote() });

    public Task SetRemoteUrlAsync(string repositoryPath, string name, string url)
        => RunAsync(repositoryPath, _ => new GitArgumentBuilder("remote") { "set-url", name.Quote(), url.Quote() });

    // Upstream's FormDeleteTag pushes an empty ref to the tag: "push <remote> :refs/tags/<tag>".
    public Task DeleteRemoteTagAsync(string repositoryPath, string remote, string name,
        IProgress<GitOutputLine>? output = null,
        CancellationToken cancellationToken = default)
        => RunRemoteAsync(repositoryPath,
            _ => new GitArgumentBuilder("push") { "--progress", remote.Quote(), $":refs/tags/{name}".Quote() },
            output, cancellationToken);

    // The same arguments as upstream's FormCreateWorktree and FormManageWorktree.
    public Task AddWorktreeAsync(string repositoryPath, string path, string branch, string newBranch)
        => RunAsync(repositoryPath,
            _ => new GitArgumentBuilder("worktree")
            {
                "add",
                path.ToPosixPath().Quote(),
                { newBranch.Length > 0, $"-b {newBranch.Quote()}" },
                branch.Quote()
            });

    public Task RemoveWorktreeAsync(string repositoryPath, string path, bool force)
        => RunAsync(repositoryPath,
            _ => new GitArgumentBuilder("worktree") { "remove", { force, "--force" }, path.ToPosixPath().Quote() });

    public Task PruneWorktreesAsync(string repositoryPath)
        => RunAsync(repositoryPath, _ => new GitArgumentBuilder("worktree") { "prune" });

    public Task UpdateSubmodulesAsync(string repositoryPath, string? path, IProgress<GitOutputLine>? output = null,
        CancellationToken cancellationToken = default)
        => RunRemoteAsync(repositoryPath, _ => Commands.SubmoduleUpdate(path), output, cancellationToken);

    public Task SyncSubmodulesAsync(string repositoryPath, string? path)
        => RunAsync(repositoryPath, _ => Commands.SubmoduleSync(path));

    public Task DiscardChangesAsync(string repositoryPath, IReadOnlyList<string> paths)
        => RunAsync(repositoryPath, _ =>
        {
            GitArgumentBuilder checkout = new("checkout") { "--" };
            foreach (string path in paths)
            {
                checkout.Add(path.Quote());
            }

            return checkout;
        });

    public Task ResolveConflictsAsync(string repositoryPath, IReadOnlyList<string> paths, bool ours)
        => Task.Run(() =>
        {
            GitModule module = CreateModule(repositoryPath);
            GitArgumentBuilder checkout = new("checkout") { ours ? "--ours" : "--theirs", "--" };
            GitArgumentBuilder add = new("add") { "--" };
            foreach (string path in paths)
            {
                checkout.Add(path.Quote());
                add.Add(path.Quote());
            }

            Execute(module.GitExecutable, checkout);
            Execute(module.GitExecutable, add);
        });

    public Task RunMergeToolAsync(string repositoryPath, string path)
        => Task.Run(() =>
        {
            GitModule module = CreateModule(repositoryPath);
            string tool = module.GetEffectiveSetting("merge.guitool");
            if (tool.Length == 0)
            {
                tool = module.GetEffectiveSetting("merge.tool");
            }

            if (tool.Length == 0)
            {
                throw new GitOperationException(
                    "No merge tool is configured. Set merge.tool in Settings, then try again.");
            }

            // --no-prompt: there is no console to answer git's "Hit return to start" question.
            Execute(module.GitExecutable,
                new GitArgumentBuilder("mergetool")
                {
                    "--no-prompt", $"--tool={tool}".Quote(), "--", path.ToPosixPath().Quote()
                });
        });

    // Like the merge tool: a configured GUI tool is required, as git's fallback is a terminal program with no console here.
    // git difftool waits for the tool, so it is started and not awaited.
    public Task RunDiffToolAsync(string repositoryPath, string path, string? commit, bool staged)
        => Task.Run(() =>
        {
            GitModule module = CreateModule(repositoryPath);
            string tool = module.GetEffectiveSetting("diff.guitool");
            if (tool.Length == 0)
            {
                tool = module.GetEffectiveSetting("diff.tool");
            }

            if (tool.Length == 0)
            {
                throw new GitOperationException(
                    "No diff tool is configured. Set diff.tool in Settings, then try again.");
            }

            GitArgumentBuilder arguments = new("difftool")
            {
                "--no-prompt",
                $"--tool={tool}".Quote(),
                { staged && commit is null, "--cached" },
                { commit is not null, $"{commit}^!" },
                "--",
                path.ToPosixPath().Quote()
            };
            IProcess process = module.GitExecutable.Start(arguments, throwOnErrorExit: false);
            _ = process.WaitForExitAsync().ContinueWith(_ => process.Dispose(), TaskScheduler.Default);
        });

    public Task StashAsync(string repositoryPath, string message, bool includeUntracked, bool keepIndex,
        IReadOnlyList<string>? paths = null)
        => RunAsync(repositoryPath,
            _ => Commands.StashSave(includeUntracked, keepIndex, message, selectedFiles: paths));

    public Task ApplyStashAsync(string repositoryPath, string stashName)
        => RunAsync(repositoryPath, _ => new GitArgumentBuilder("stash") { "apply", "-q", stashName.Quote() });

    public Task PopStashAsync(string repositoryPath, string stashName)
        => RunAsync(repositoryPath, _ => new GitArgumentBuilder("stash") { "pop", "-q", stashName.Quote() });

    public Task DropStashAsync(string repositoryPath, string stashName)
        => RunAsync(repositoryPath, _ => new GitArgumentBuilder("stash") { "drop", "-q", stashName.Quote() });

    public Task CreateTagAsync(string repositoryPath, string name, string commit, string message)
        => RunAsync(repositoryPath, _ => string.IsNullOrWhiteSpace(message)
            ? new GitArgumentBuilder("tag") { name.Quote(), commit.Quote() }
            : new GitArgumentBuilder("tag")
            {
                "-a",
                name.Quote(),
                "-m",
                message.Quote(),
                commit.Quote()
            });

    public Task DeleteTagAsync(string repositoryPath, string name)
        => RunAsync(repositoryPath, _ => new GitArgumentBuilder("tag") { "-d", name.Quote() });

    public Task CherryPickAsync(string repositoryPath, string commit)
        => RunAsync(repositoryPath, _ => new GitArgumentBuilder("cherry-pick") { commit.Quote() });

    public Task ResetAsync(string repositoryPath, string commit, ResetMode mode)
        => RunAsync(repositoryPath, _ => Commands.Reset(mode, commit));

    public Task RevertAsync(string repositoryPath, string commit)
        => RunAsync(repositoryPath, _ => new GitArgumentBuilder("revert") { "--no-edit", commit.Quote() });

    public Task RebaseAsync(string repositoryPath, string branch)
        => RunAsync(repositoryPath, _ => new GitArgumentBuilder("rebase") { branch.Quote() });

    public Task AbortRebaseAsync(string repositoryPath)
        => RunAsync(repositoryPath, _ => new GitArgumentBuilder("rebase") { "--abort" });

    public Task RebaseInteractiveAsync(string repositoryPath, string onto, string editorCommand,
        IProgress<GitOutputLine>? output = null, CancellationToken cancellationToken = default)
        => Task.Run(async () =>
        {
            GitModule module = CreateModule(repositoryPath);
            ArgumentString rebase = Commands.Rebase(new Commands.RebaseOptions
            {
                BranchName = onto,
                Interactive = true,
                AutoSquash = module.GetEffectiveSetting<bool>("rebase.autosquash") is true,
            });
            await GitOutputRunner.RunAsync(module.WorkingDir, rebase, output ?? NullOutput.Instance, cancellationToken,
                GitEditorCommand.Environment(editorCommand));
        }, cancellationToken);

    public Task ContinueRebaseAsync(string repositoryPath, string? editorCommand = null)
        => RunRebaseStepAsync(repositoryPath, Commands.ContinueRebase(), editorCommand);

    public Task SkipRebaseAsync(string repositoryPath, string? editorCommand = null)
        => RunRebaseStepAsync(repositoryPath, Commands.SkipRebase(), editorCommand);

    public Task EditRebaseTodoAsync(string repositoryPath, string editorCommand)
        => Task.Run(async () =>
        {
            GitModule module = CreateModule(repositoryPath);
            await GitOutputRunner.RunAsync(module.WorkingDir, Commands.EditTodoRebase(), NullOutput.Instance,
                CancellationToken.None, GitEditorCommand.Environment(editorCommand));
        });

    public Task ContinueMergeAsync(string repositoryPath, string? editorCommand = null)
        => RunRebaseStepAsync(repositoryPath, Commands.ContinueMerge(), editorCommand);

    public Task ContinuePatchAsync(string repositoryPath)
        => RunAsync(repositoryPath, _ => Commands.Resolved());

    public Task SkipPatchAsync(string repositoryPath)
        => RunAsync(repositoryPath, _ => Commands.Skip());

    public Task AbortPatchAsync(string repositoryPath)
        => RunAsync(repositoryPath, _ => Commands.Abort());

    public Task StartBisectAsync(string repositoryPath, IProgress<GitOutputLine>? output = null,
        CancellationToken cancellationToken = default)
        => RunWithOutputAsync(repositoryPath, Commands.StartBisect(), output, cancellationToken);

    public Task MarkBisectAsync(string repositoryPath, GitBisectOption option, string? commit,
        IProgress<GitOutputLine>? output = null, CancellationToken cancellationToken = default)
        => RunWithOutputAsync(repositoryPath,
            commit is null
                ? Commands.ContinueBisect(option)
                : Commands.ContinueBisect(option, ObjectId.Parse(commit)),
            output, cancellationToken);

    public Task StopBisectAsync(string repositoryPath, IProgress<GitOutputLine>? output = null,
        CancellationToken cancellationToken = default)
        => RunWithOutputAsync(repositoryPath, Commands.StopBisect(), output, cancellationToken);

    public Task CloneAsync(string sourceUrl, string targetPath, IProgress<GitOutputLine>? output = null,
        CancellationToken cancellationToken = default, int? depth = null)
        => Task.Run(async () =>
        {
            string fullTarget = Path.GetFullPath(targetPath);
            string parent = Path.GetDirectoryName(fullTarget)
                            ?? throw new InvalidOperationException($"Cannot clone into {targetPath}");
            Directory.CreateDirectory(parent);

            string source = PathUtil.IsLocalFile(sourceUrl) ? sourceUrl.ToPosixPath() : sourceUrl;
            ArgumentString clone = Commands.Clone(source, fullTarget, path => path?.ToPosixPath(), depth: depth);
            if (output is null)
            {
                Execute(new Executable(AppSettings.GitCommand, parent), clone);
            }
            else
            {
                await GitOutputRunner.RunAsync(parent, clone, output, cancellationToken, _remoteEnvironment);
            }
        }, cancellationToken);

    public Task InitAsync(string folder)
        => Task.Run(() =>
        {
            string fullPath = Path.GetFullPath(folder);
            Directory.CreateDirectory(fullPath);
            Execute(new Executable(AppSettings.GitCommand, fullPath), new GitArgumentBuilder("init") { "-q" });
        });

    // With an output, git runs through GitOutputRunner so its progress is seen while it runs and it can be stopped, and
    // its prompts are answered by the app.
    private Task RunRemoteAsync(string repositoryPath, Func<GitModule, ArgumentString> build,
        IProgress<GitOutputLine>? output,
        CancellationToken cancellationToken)
        => Task.Run(async () =>
        {
            GitModule module = CreateModule(repositoryPath);
            if (output is null)
            {
                Execute(module.GitExecutable, build(module));
            }
            else
            {
                await GitOutputRunner.RunAsync(module.WorkingDir, build(module), output, cancellationToken,
                    _remoteEnvironment);
            }
        }, cancellationToken);

    // With the app's editor, git opens it for the messages of the steps that follow, as upstream does through core.editor.
    // Without one the step runs with core.editor=true, so git keeps each message instead of waiting for an editor nobody
    // sees. (A plain rebase also writes rebase-merge/interactive, so that file cannot tell the two kinds apart.)
    private static Task RunRebaseStepAsync(string repositoryPath, ArgumentString step, string? editorCommand)
        => Task.Run(async () =>
        {
            GitModule module = CreateModule(repositoryPath);
            if (editorCommand is not null)
            {
                await GitOutputRunner.RunAsync(module.WorkingDir, step, NullOutput.Instance, CancellationToken.None,
                    GitEditorCommand.Environment(editorCommand));
                return;
            }

            Execute(module.GitExecutable, (ArgumentString)$"-c core.editor=true {step}");
        });

    private static Task RunWithOutputAsync(string repositoryPath, ArgumentString arguments,
        IProgress<GitOutputLine>? output, CancellationToken cancellationToken)
        => Task.Run(
            () => GitOutputRunner.RunAsync(CreateModule(repositoryPath).WorkingDir, arguments,
                output ?? NullOutput.Instance, cancellationToken),
            cancellationToken);

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

/// <summary>
///  Drops git's output when no view shows it.
/// </summary>
internal sealed class NullOutput : IProgress<GitOutputLine>
{
    public static readonly NullOutput Instance = new();

    public void Report(GitOutputLine value)
    {
    }
}
