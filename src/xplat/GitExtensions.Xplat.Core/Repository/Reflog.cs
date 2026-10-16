using System.Text.RegularExpressions;
using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils;

namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  One line of a reflog (upstream's <c>RefLine</c>): the commit, the ref selector (e.g. <c>HEAD@{0}</c>) and the action
///  that moved the ref there.
/// </summary>
public sealed record ReflogEntry(ObjectId Sha, string Ref, string Action)
{
    public string ShortHash => Sha.ToShortString();
}

/// <summary>
///  Upstream's <c>FormReflog</c> reading: the references it offers and a reference's reflog, so a commit that a reset or
///  rebase moved away from can be found again.
/// </summary>
public static partial class Reflog
{
    public const string Head = "HEAD";

    [GeneratedRegex(@"^(?<sha>[^ ]+) (?<ref>[^:]+): (?<action>.+)$", RegexOptions.ExplicitCapture)]
    private static partial Regex ReflogRegex { get; }

    /// <summary>
    ///  Upstream's <c>FormReflog_Load</c> list: HEAD, then the local branches and the remote branches, each by name.
    /// </summary>
    public static IReadOnlyList<string> References(IGitModule module)
        =>
        [
            Head,
            .. module.GetRefs(RefsFilter.Heads).Select(gitRef => gitRef.Name).OrderBy(name => name),
            .. module.GetRemoteBranches().Select(gitRef => gitRef.Name).OrderBy(name => name),
        ];

    /// <summary>
    ///  Upstream's <c>DisplayRefLogAsync</c>: <c>git reflog --no-abbrev</c> of <paramref name="reference"/>, the lines its
    ///  pattern matches.
    /// </summary>
    public static Task<IReadOnlyList<ReflogEntry>> LoadAsync(string repositoryPath, string reference = Head)
        => Task.Run(() =>
        {
            GitModule module = GitModules.Open(repositoryPath);
            string output = module.GitExecutable.GetOutput(new GitArgumentBuilder("reflog") { "--no-abbrev", reference });
            return Parse(output);
        });

    public static IReadOnlyList<ReflogEntry> Parse(string output)
        =>
        [
            .. from line in output.LazySplit('\n')
               where line.Length != 0
               select ReflogRegex.Match(line)
               into match
               where match.Success
               select new ReflogEntry(ObjectId.Parse(match.Groups["sha"].Value), match.Groups["ref"].Value,
                   match.Groups["action"].Value),
        ];
}
