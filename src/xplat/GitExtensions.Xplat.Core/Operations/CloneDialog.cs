using GitCommands;
using GitCommands.Config;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  Upstream's <c>FormClone</c> without its window: the repository to clone, the destination and the subdirectory to
///  create in it, the branch, the options and the repository type; the line that says where the clone goes, the checks
///  before cloning and the clone command.
/// </summary>
public sealed class CloneDialog
{
    // Upstream's texts.
    public const string InfoDirectoryExists = "(Directory already exists)";
    public const string InfoDirectoryNew = "(New directory)";
    public const string QuestionOpenRepoCaption = "Open";
    public const string BranchDefaultRemoteHead = "(default: remote HEAD)";
    public const string BranchNone = "(none: don't checkout after clone)";
    public const string ErrorDestinationNotSupplied = "You need to specify destination folder.";
    public const string ErrorDestinationNotRooted = "Destination folder must be an absolute path.";
    public const string ErrorCloneFailed = "Clone Failed";

    // Upstream's labels, without their access key marks, which upstream's location line puts in brackets for a missing
    // value.
    public const string DestinationLabel = "Destination:";
    public const string SubdirectoryLabel = "Subdirectory to create:";

    public static readonly string InfoNewRepositoryLocation =
        "The repository will be cloned to a new directory located here:" + Environment.NewLine + "{0}";

    public static readonly string QuestionOpenRepo =
        "The repository has been cloned successfully." + Environment.NewLine +
        "Do you want to open the new repository \"{0}\" now?";

    private const string Origin = "origin";

    private string _from = "";

    /// <summary>
    ///  Upstream's branch choices before the remote's branches are read: the remote's HEAD, or no checkout.
    /// </summary>
    public static IReadOnlyList<string> DefaultBranchItems { get; } = [BranchDefaultRemoteHead, BranchNone];

    /// <summary>
    ///  The repository to clone, a URL or a local path. Setting it proposes the subdirectory, as upstream's
    ///  <c>FromTextUpdate</c>.
    /// </summary>
    public string From
    {
        get => _from;
        set
        {
            _from = value;
            string name = PathUtil.GetRepositoryName(value);
            if (name != "")
            {
                Subdirectory = name;
            }
        }
    }

    public string Destination { get; set; } = "";

    public string Subdirectory { get; set; } = "";

    /// <summary>
    ///  The branch box's text: a branch, or one of <see cref="DefaultBranchItems"/>.
    /// </summary>
    public string Branch { get; set; } = BranchDefaultRemoteHead;

    /// <summary>
    ///  Upstream's "Public repository, no working directory (--bare)"; false for "Personal repository".
    /// </summary>
    public bool Bare { get; set; }

    public bool InitializeAllSubmodules { get; set; } = true;

    public bool DownloadFullHistory { get; set; } = true;

    /// <summary>
    ///  Upstream's <c>OnRuntimeLoad</c>: the repository to clone and the destination the dialog starts with. A URL given to
    ///  the dialog is used as is; otherwise a folder given is the destination, a URL in the clipboard is proposed, else
    ///  the open repository's remote (so the new clone is likely on the same server), and the destination falls back to
    ///  the open repository's parent folder.
    /// </summary>
    /// <param name="url">The URL or folder the dialog was opened with; null for none.</param>
    /// <param name="defaultDestination">Upstream's <c>DefaultCloneDestinationPath</c>.</param>
    /// <param name="clipboardText">The clipboard's text; null when there is none or it cannot be read.</param>
    /// <param name="module">The open repository; null when none is open.</param>
    public static (string From, string Destination) Initial(string? url, string defaultDestination,
        string? clipboardText,
        IGitModule? module)
    {
        string from = "";
        string destination = defaultDestination;
        if (PathUtil.CanBeGitURL(url))
        {
            from = url!;
        }
        else
        {
            if (!string.IsNullOrEmpty(url) && Directory.Exists(url))
            {
                destination = url;
            }

            if (clipboardText is not null && TryExtractUrl(clipboardText, out string possibleUrl))
            {
                from = possibleUrl;
            }

            if (string.IsNullOrWhiteSpace(from) && module is not null && module.IsValidGitWorkingDir())
            {
                string? remote =
                    module.GetSetting(string.Format(SettingKeyString.BranchRemote, module.GetSelectedBranch()));
                if (string.IsNullOrEmpty(remote))
                {
                    IReadOnlyList<string> remotes = module.GetRemoteNames();
                    remote = remotes.Any(name => name.Equals(Origin, StringComparison.InvariantCultureIgnoreCase))
                        ? Origin
                        : remotes.Count > 0
                            ? remotes[0]
                            : null;
                }

                string pushUrl = module.GetSetting(string.Format(SettingKeyString.RemotePushUrl, remote));
                if (string.IsNullOrEmpty(pushUrl))
                {
                    pushUrl = module.GetSetting(string.Format(SettingKeyString.RemoteUrl, remote));
                }

                from = pushUrl;
                if (!string.IsNullOrWhiteSpace(pushUrl) && string.IsNullOrWhiteSpace(destination)
                                                        && !string.IsNullOrWhiteSpace(module.WorkingDir))
                {
                    destination = ParentFolder(module.WorkingDir) ?? destination;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(destination) && module is not null &&
            !string.IsNullOrWhiteSpace(module.WorkingDir))
        {
            if (!module.IsValidGitWorkingDir())
            {
                destination = module.WorkingDir;
            }
            else if (Path.GetPathRoot(module.WorkingDir) != module.WorkingDir)
            {
                destination = ParentFolder(module.WorkingDir) ?? destination;
            }
        }

        return (from, destination);
    }

    /// <summary>
    ///  Upstream's <c>TryExtractUrl</c>: the first space separated word of <paramref name="contents"/> that can be a git
    ///  URL.
    /// </summary>
    public static bool TryExtractUrl(string contents, out string url)
    {
        url = contents.Split(' ').FirstOrDefault(PathUtil.CanBeGitURL) ?? "";
        return url.Length > 0;
    }

    /// <summary>
    ///  Upstream's <c>ToTextUpdate</c>: where the clone goes, and whether that is a problem (a missing or invalid value, or
    ///  a folder that exists and is not empty), which upstream shows in red.
    /// </summary>
    /// <param name="translate">The translation of upstream's texts; none when null.</param>
    public (string Text, bool IsError) LocationInfo(Func<string, string>? translate = null)
    {
        translate ??= text => text;
        bool destinationUnfilled = string.IsNullOrEmpty(Destination)
                                   || Destination.AsSpan().IndexOfAny(Delimiters.InvalidPathCharsSearchValues) >= 0;
        bool subdirectoryUnfilled = string.IsNullOrEmpty(Subdirectory)
                                    || Subdirectory.AsSpan().IndexOfAny(Delimiters.InvalidPathCharsSearchValues) >= 0;
        string path = Path.Combine(destinationUnfilled ? $"[{translate(DestinationLabel)}]" : Destination,
            subdirectoryUnfilled ? $"[{translate(SubdirectoryLabel)}]" : Subdirectory);
        string info = string.Format(translate(InfoNewRepositoryLocation), path);
        if (destinationUnfilled || subdirectoryUnfilled)
        {
            return (info, true);
        }

        return Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any()
            ? ($"{info} {translate(InfoDirectoryExists)}", true)
            : ($"{info} {translate(InfoDirectoryNew)}", false);
    }

    /// <summary>
    ///  Upstream's <c>OkClick</c> checks: a destination is given and absolute. Returns the error to show, or null.
    /// </summary>
    public string? Validate()
        => string.IsNullOrWhiteSpace(Destination) ? ErrorDestinationNotSupplied
            : !Path.IsPathRooted(Destination) ? ErrorDestinationNotRooted
            : null;

    /// <summary>
    ///  The folder the clone is made in: the subdirectory of the destination, resolved as upstream resolves it (which
    ///  throws for an invalid path).
    /// </summary>
    public string TargetPath() => PathUtil.Resolve(Path.Combine(Destination, Subdirectory));

    /// <summary>
    ///  Upstream's <c>Commands.Clone</c> as <c>OkClick</c> calls it: without the full history, a clone of depth 1 that
    ///  still fetches every branch (upstream's reason: switching branches in a single branch clone is hard); the remote's
    ///  HEAD is checked out unless a branch or no checkout is chosen.
    /// </summary>
    /// <param name="targetPath">The folder from <see cref="TargetPath"/>.</param>
    /// <param name="getPathForGitExecution">The target repository's path conversion for git.</param>
    public ArgumentString Arguments(string targetPath, Func<string?, string?> getPathForGitExecution)
    {
        string? branch = Branch == BranchDefaultRemoteHead ? "" : Branch == BranchNone ? null : Branch;
        return Commands.Clone(From, targetPath, getPathForGitExecution, Bare, InitializeAllSubmodules, branch,
            DownloadFullHistory ? null : 1, DownloadFullHistory ? null : false);
    }

    /// <summary>
    ///  Upstream's destination choices (<c>OnRuntimeLoad</c>): the parent folders of the recent local repositories
    ///  (upstream's <c>GetParentPath</c>, empty for a folder that is gone or on a network share), once each, ignoring case.
    /// </summary>
    public static IReadOnlyList<string> DestinationsFromRecent(IEnumerable<string> recentRepositories)
        =>
        [
            .. recentRepositories.Select(path => new GitCommands.UserRepositoryHistory.Repository(path).GetParentPath())
                .Where(path => !string.IsNullOrEmpty(path))
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
        ];

    /// <summary>
    ///  Upstream's <c>UpdateBranches</c>: the branch choices with the remote's branches after the defaults.
    /// </summary>
    public static IReadOnlyList<string> BranchItems(IEnumerable<IGitRef> remoteBranches)
        => [.. DefaultBranchItems, .. remoteBranches.Select(branch => branch.LocalName)];

    // Upstream's Path.GetDirectoryName of the working directory without its trailing separator; upstream ignores a path
    // it cannot use.
    private static string? ParentFolder(string workingDir)
    {
        try
        {
            return Path.GetDirectoryName(workingDir.TrimEnd(Path.DirectorySeparatorChar));
        }
        catch (Exception ex) when (ex is ArgumentException or PathTooLongException)
        {
            return null;
        }
    }
}
