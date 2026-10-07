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
    Task<IReadOnlyList<DiffLine>> GetDiffAsync(string repositoryPath, string? commitHash, string? filePath, bool staged, DiffOptions? options = null);
}

public sealed class GitDiffService : IDiffService
{
    public Task<IReadOnlyList<DiffLine>> GetDiffAsync(string repositoryPath, string? commitHash, string? filePath, bool staged, DiffOptions? options = null)
        => Task.Run(() =>
        {
            GitModule module = new(new GitExecutorProvider(new GitDirectoryResolver()), repositoryPath);
            string output = Run(module, options ?? new DiffOptions(), commitHash is null
                ? new GitArgumentBuilder("diff") { { staged, "--cached" } }
                : new GitArgumentBuilder("show") { "--format=", "--first-parent", commitHash }, filePath);

            if (output.Length == 0 && commitHash is null && !staged && filePath is not null && IsUntracked(module, filePath))
            {
                output = RunNoIndexDiff(module, filePath);
            }

            return DiffParser.Parse(output);
        });

    private static string Run(GitModule module, DiffOptions options, GitArgumentBuilder arguments, string? filePath)
    {
        arguments.Add("--no-color");
        arguments.Add("--no-ext-diff");
        foreach (string option in options.Arguments)
        {
            arguments.Add(option);
        }

        if (filePath is not null)
        {
            arguments.Add("--");
            arguments.Add(filePath.Quote());
        }

        ExecutionResult result = module.GitExecutable.Execute(arguments, throwOnErrorExit: false);
        if (!result.ExitedSuccessfully)
        {
            string error = result.StandardError.Trim();
            throw new GitOperationException(error.Length > 0 ? error : $"git diff failed with exit code {result.ExitCode}");
        }

        return result.StandardOutput;
    }

    private static bool IsUntracked(GitModule module, string filePath)
    {
        ExecutionResult result = module.GitExecutable.Execute(
            new GitArgumentBuilder("ls-files") { "--error-unmatch", "--", filePath.Quote() }, throwOnErrorExit: false);
        return !result.ExitedSuccessfully;
    }

    // An untracked file has no diff against the index, so it is shown as added from /dev/null. git exits with 1 when the files differ.
    private static string RunNoIndexDiff(GitModule module, string filePath)
    {
        ExecutionResult result = module.GitExecutable.Execute(
            new GitArgumentBuilder("diff") { "--no-index", "--no-color", "--no-ext-diff", "--", "/dev/null", filePath.Quote() },
            throwOnErrorExit: false);
        if (result.ExitCode is not (0 or 1))
        {
            string error = result.StandardError.Trim();
            throw new GitOperationException(error.Length > 0 ? error : $"git diff failed with exit code {result.ExitCode}");
        }

        return result.StandardOutput;
    }
}
