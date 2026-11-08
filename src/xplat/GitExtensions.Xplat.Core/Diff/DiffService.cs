using GitCommands;
using GitCommands.Git;
using GitCommands.Settings;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
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
    private const string DiffCommand = "diff";
    private const string ShowCommand = "show";

    public Task<IReadOnlyList<DiffLine>> GetDiffAsync(string repositoryPath, string? commitHash, string? filePath, bool staged, DiffOptions? options = null)
        => Task.Run<IReadOnlyList<DiffLine>>(async () =>
        {
            GitModule module = new(new GitExecutorProvider(new GitDirectoryResolver()), repositoryPath);
            DiffOptions diffOptions = options ?? new DiffOptions();
            if (diffOptions.Appearance == DiffDisplayAppearance.Difftastic && filePath is not null && Difftastic.IsEnabled(module)
                && !Directory.Exists(Path.Combine(module.WorkingDir, filePath))
                && DifftasticRevisions(module, commitHash, staged) is var (first, second))
            {
                return await Difftastic.DiffAsync(module, first, second, filePath, oldPath: null,
                    commitHash is not null || staged || !IsUntracked(module, filePath), diffOptions);
            }

            string output = Run(module, diffOptions, commitHash is null
                ? new GitArgumentBuilder(DiffCommand, Configuration(module, diffOptions, DiffCommand)) { { staged, "--cached" } }
                : new GitArgumentBuilder(ShowCommand, Configuration(module, diffOptions, ShowCommand))
                {
                    "--format=", "--first-parent", commitHash
                }, filePath);

            if (output.Length == 0 && commitHash is null && !staged && filePath is not null && IsUntracked(module, filePath))
            {
                output = RunNoIndexDiff(module, filePath, diffOptions);
            }

            IReadOnlyList<DiffLine> lines = diffOptions.UsesGitColors
                ? GitColoring.ParseDiff(output, isGitWordDiff: diffOptions.IsGitWordDiff)
                : DiffParser.Parse(output);
            if (filePath is not null && SubmoduleDiff.IsSubmoduleDiff(lines))
            {
                if (commitHash is null)
                {
                    return await SubmoduleDiff.CurrentLinesAsync(module, filePath, staged);
                }

                if (SubmoduleDiff.CommitRevisions(module, commitHash) is var (parent, commit))
                {
                    return await SubmoduleDiff.LinesAsync(module, filePath, oldPath: null, parent, commit);
                }
            }

            return InlineDifferences.Add(lines, diffOptions);
        });

    // The two sides of what the viewer shows: a commit against its first parent, the index against HEAD, the working directory
    // against the index. A commit without a parent has nothing to compare, and keeps its diff.
    private static (ObjectId First, ObjectId Second)? DifftasticRevisions(GitModule module, string? commitHash, bool staged)
    {
        if (commitHash is null)
        {
            return staged ? (module.GetCurrentCheckout(), ObjectId.IndexId) : (ObjectId.IndexId, ObjectId.WorkTreeId);
        }

        return SubmoduleDiff.CommitRevisions(module, commitHash);
    }

    // Upstream's PatchHighlightService.GetGitCommandConfiguration: git's default configuration without git's colors.
    private static IGitCommandConfiguration? Configuration(GitModule module, DiffOptions options, string command)
        => options.UsesGitColors ? GitColoring.Configuration(module, command) : null;

    private static string ColorArgument(DiffOptions options) => options.UsesGitColors ? "--color=always" : "--no-color";

    private static string Run(GitModule module, DiffOptions options, GitArgumentBuilder arguments, string? filePath)
    {
        arguments.Add(ColorArgument(options));
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

        // As upstream's GetSingleDiffAsync and GetCurrentChanges: git's output read in the viewer's encoding.
        ExecutionResult result = module.GitExecutable.Execute(arguments, outputEncoding: options.EncodingFor(module),
            stripAnsiEscapeCodes: !options.UsesGitColors, throwOnErrorExit: false);
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
    private static string RunNoIndexDiff(GitModule module, string filePath, DiffOptions options)
    {
        ExecutionResult result = module.GitExecutable.Execute(
            new GitArgumentBuilder(DiffCommand, Configuration(module, options, DiffCommand))
            {
                "--no-index", ColorArgument(options), "--no-ext-diff", "--", "/dev/null", filePath.Quote()
            },
            outputEncoding: options.EncodingFor(module), stripAnsiEscapeCodes: !options.UsesGitColors, throwOnErrorExit: false);
        if (result.ExitCode is not (0 or 1))
        {
            string error = result.StandardError.Trim();
            throw new GitOperationException(error.Length > 0 ? error : $"git diff failed with exit code {result.ExitCode}");
        }

        return result.StandardOutput;
    }
}
