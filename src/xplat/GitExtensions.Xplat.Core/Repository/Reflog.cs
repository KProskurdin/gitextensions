using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtUtils;

namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  One entry of HEAD's reflog: where HEAD pointed, and the action that moved it there.
/// </summary>
public sealed record ReflogEntry(string Hash, string ShortHash, string Selector, string Message);

/// <summary>
///  Reads HEAD's reflog, so a commit that a reset or rebase moved away from can be found again.
/// </summary>
public static class Reflog
{
    private const string Format = "--format=%H%x09%h%x09%gd%x09%gs";
    private const char Tab = '\t';

    public static Task<IReadOnlyList<ReflogEntry>> LoadAsync(string repositoryPath, int limit)
        => Task.Run(() =>
        {
            GitModule module = new(new GitExecutorProvider(new GitDirectoryResolver()), repositoryPath);
            ExecutionResult result = module.GitExecutable.Execute(
                new GitArgumentBuilder("reflog") { "-n", limit.ToString(System.Globalization.CultureInfo.InvariantCulture), Format },
                throwOnErrorExit: false);
            if (!result.ExitedSuccessfully)
            {
                throw new InvalidOperationException(result.StandardError.Trim());
            }

            return (IReadOnlyList<ReflogEntry>)result.StandardOutput
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Split(Tab))
                .Where(parts => parts.Length == 4)
                .Select(parts => new ReflogEntry(parts[0], parts[1], parts[2], parts[3]))
                .ToList();
        });
}
