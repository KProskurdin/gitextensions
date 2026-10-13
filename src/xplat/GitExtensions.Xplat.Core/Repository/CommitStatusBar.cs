using GitCommands;
using GitCommands.Config;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;

namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  Upstream FormCommit's branch display (<c>UpdateBranchNameDisplayAsync</c>): the checked out branch, with an arrow and
///  where it pushes to when it is a branch (<see cref="PushTo"/> null otherwise, e.g. a detached HEAD).
/// </summary>
public sealed record CommitBranchStatus(string Branch, string? PushTo)
{
    /// <summary>
    ///  The branch label: the name, followed by an arrow when it pushes somewhere.
    /// </summary>
    public string BranchText => PushTo is null ? Branch : $"{Branch} →";
}

/// <summary>
///  Upstream FormCommit's status strip texts (<c>commitStatusStrip</c>): the committer and author, the branch and where it
///  pushes, the staged count and the caret's line and column in the message.
/// </summary>
public static class CommitStatusBar
{
    // Upstream's FormCommit and GitUI TranslatedStrings texts.
    private const string CommitterText = "Committer";
    private const string AuthorText = "Author";
    private const string UntrackedRemoteText = "(untracked)";
    private const string RemoteNotConfiguredText = "(remote not configured)";
    private const string NotConfiguredText = "{0} not configured";
    private const string TitleText = "Commit to {0} ({1})";
    private const string DefaultRemote = "origin";

    /// <summary>
    ///  Upstream's <c>UpdateAuthorInfo</c>: "Committer name &lt;email&gt;", then the author when one is typed; a missing
    ///  setting reads "/user.name not configured/".
    /// </summary>
    public static Task<string> CommitterAsync(string repositoryPath, string author)
        => Task.Run(() =>
        {
            GitModule module = CreateModule(repositoryPath);
            string committer =
                $"{CommitterText} {Setting(module, SettingKeyString.UserName)} <{Setting(module, SettingKeyString.UserEmail)}>";
            return string.IsNullOrWhiteSpace(author) ? committer : $"{committer} {AuthorText} {author}";
        });

    /// <summary>
    ///  Upstream's <c>UpdateBranchNameDisplayAsync</c>: a branch pushes to the remote branch it tracks, or else to the same
    ///  name on origin (or the first remote) marked "(untracked)", or "(remote not configured)" without remotes.
    /// </summary>
    public static Task<CommitBranchStatus> BranchAsync(string repositoryPath)
        => Task.Run(() =>
        {
            GitModule module = CreateModule(repositoryPath);
            string branch = module.GetSelectedBranch();
            IGitRef? head = module.GetRefs(RefsFilter.Heads).FirstOrDefault(reference => reference.LocalName == branch);
            if (head is null)
            {
                return new CommitBranchStatus(branch, PushTo: null);
            }

            if (!string.IsNullOrEmpty(head.TrackingRemote))
            {
                return new CommitBranchStatus(branch, $"{head.TrackingRemote}/{head.MergeWith}");
            }

            IReadOnlyList<string> remotes = module.GetRemoteNames();
            string? remote = remotes.FirstOrDefault(name => name == DefaultRemote)
                             ?? remotes.Order(StringComparer.Ordinal).FirstOrDefault();
            return new CommitBranchStatus(branch,
                remote is null ? RemoteNotConfiguredText : $"{remote}/{branch} {UntrackedRemoteText}");
        });

    /// <summary>
    ///  Upstream's window title: "Commit to &lt;branch&gt; (&lt;working directory&gt;)".
    /// </summary>
    public static string Title(string branch, string repositoryPath)
        => string.Format(TitleText, branch, PathUtil.GetDisplayPath(repositoryPath.EnsureTrailingPathSeparator()));

    /// <summary>
    ///  Upstream's staged count: the staged files of all changed files, e.g. "1/3".
    /// </summary>
    public static string Staged(int staged, int total) => $"{staged}/{total}";

    /// <summary>
    ///  Upstream's <c>CurrentLine</c> and <c>CurrentColumn</c> of the message box: one-based, for the caret at
    ///  <paramref name="caretIndex"/>.
    /// </summary>
    public static (int Line, int Column) Caret(string text, int caretIndex)
    {
        int index = Math.Clamp(caretIndex, 0, text.Length);
        int lineStart = index == 0 ? 0 : text.LastIndexOf('\n', index - 1) + 1;
        int line = text.AsSpan(0, index).Count('\n') + 1;
        return (line, index - lineStart + 1);
    }

    private static string Setting(GitModule module, string key)
        => module.GetEffectiveSetting(key) is { Length: > 0 } value
            ? value
            : $"/{string.Format(NotConfiguredText, key)}/";

    private static GitModule CreateModule(string path) =>
        new(new GitExecutorProvider(new GitDirectoryResolver()), path);
}
