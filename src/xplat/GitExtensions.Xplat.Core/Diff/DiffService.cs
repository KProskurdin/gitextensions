using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Xplat.Core.Operations;
using GitExtUtils;

namespace GitExtensions.Xplat.Core.Diff;

public interface IDiffService
{
    /// <summary>
    ///  The diff of <paramref name="filePath"/> in <paramref name="commitHash"/>, or of the working tree (staged or unstaged)
    ///  when no commit is given.
    /// </summary>
    Task<IReadOnlyList<DiffLine>> GetDiffAsync(string repositoryPath, string? commitHash, string filePath, bool staged);
}

public sealed class GitDiffService : IDiffService
{
    public Task<IReadOnlyList<DiffLine>> GetDiffAsync(string repositoryPath, string? commitHash, string filePath, bool staged)
        => Task.Run(() =>
        {
            GitModule module = new(new GitExecutorProvider(new GitDirectoryResolver()), repositoryPath);
            GitArgumentBuilder arguments = commitHash is null
                ? new GitArgumentBuilder("diff") { { staged, "--cached" } }
                : new GitArgumentBuilder("show") { "--format=", commitHash };
            arguments.Add("--no-color");
            arguments.Add("--no-ext-diff");
            arguments.Add("--");
            arguments.Add(filePath.Quote());

            ExecutionResult result = module.GitExecutable.Execute(arguments, throwOnErrorExit: false);
            if (!result.ExitedSuccessfully)
            {
                string error = result.StandardError.Trim();
                throw new GitOperationException(error.Length > 0 ? error : $"git diff failed with exit code {result.ExitCode}");
            }

            return DiffParser.Parse(result.StandardOutput);
        });
}
