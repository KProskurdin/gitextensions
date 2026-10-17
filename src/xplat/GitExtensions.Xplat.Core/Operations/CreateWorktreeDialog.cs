using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  Upstream's <c>FormCreateWorktree</c> without its window: an existing branch or a new one, the folder proposed from
///  the main worktree's and the branch's names, when the worktree can be created and the command that creates it.
/// </summary>
public sealed class CreateWorktreeDialog
{
    /// <summary>
    ///  Upstream's <c>TranslatedStrings.LoadingData</c>, shown in the branch box while the branches load.
    /// </summary>
    public const string LoadingData = "Loading data...";

    private const string Command = "worktree";
    private const string UseRelativePathsKey = "worktree.useRelativePaths";

    private readonly string _initialDirectoryPath;
    private readonly IReadOnlyList<string> _existingBranches;
    private readonly char[] _invalidCharsInPath = Path.GetInvalidFileNameChars();

    /// <param name="initialDirectoryPath">The main worktree's folder, which the proposed folder is named after.</param>
    /// <param name="existingBranches">The local branches (upstream's <c>GetRefs(RefsFilter.Heads)</c>).</param>
    /// <param name="currentBranch">The checked out branch, which upstream leaves out of the existing branches.</param>
    public CreateWorktreeDialog(string initialDirectoryPath, IReadOnlyList<string> existingBranches,
        string currentBranch)
    {
        _initialDirectoryPath = initialDirectoryPath;
        _existingBranches = existingBranches;
        Branches = [.. existingBranches.Where(branch => branch != currentBranch)];
        SelectedBranch = Branches.FirstOrDefault();
        CreateNewBranch = !CanCheckoutExistingBranch;
        Directory = ProposedDirectory();
    }

    /// <summary>
    ///  The branches "Checkout an existing branch" offers.
    /// </summary>
    public IReadOnlyList<string> Branches { get; }

    /// <summary>
    ///  Upstream disables "Checkout an existing branch" when no other branch exists.
    /// </summary>
    public bool CanCheckoutExistingBranch => Branches.Count > 0;

    public bool CreateNewBranch { get; set; }

    public string? SelectedBranch { get; set; }

    public string NewBranchName { get; set; } = "";

    public string Directory { get; set; }

    /// <summary>
    ///  Upstream's <c>UpdateWorktreePath</c>: the main worktree's folder followed by "_" and the branch's name, with the
    ///  characters a file name cannot hold replaced.
    /// </summary>
    public string ProposedDirectory()
    {
        string branch = CreateNewBranch ? NewBranchName : SelectedBranch ?? "";
        string normalized = string.Join("_", branch.Split(_invalidCharsInPath, StringSplitOptions.RemoveEmptyEntries))
            .TrimEnd('.');
        return $"{_initialDirectoryPath}_{normalized}";
    }

    /// <summary>
    ///  Upstream's <c>ValidateWorktreeOptions</c>: a chosen branch, or a new name no branch has, and a folder that does
    ///  not exist or is empty.
    /// </summary>
    public bool CanCreate()
    {
        bool branchValid = CreateNewBranch
            ? !string.IsNullOrWhiteSpace(NewBranchName) && !_existingBranches.Contains(NewBranchName)
            : SelectedBranch is not null;
        return branchValid && IsTargetFolderValid();
    }

    /// <summary>
    ///  Upstream's <c>CreateWorktreeCommand</c>: <c>git worktree add</c> with the folder relative to the working
    ///  directory, and <c>worktree.useRelativePaths</c> on when git's config does not set it.
    /// </summary>
    /// <param name="useRelativePathsSet">Whether git's effective config sets <c>worktree.useRelativePaths</c>.</param>
    public ArgumentString Arguments(string workingDirectory, bool useRelativePathsSet)
    {
        GitCommandConfiguration configuration = new();
        foreach (GitConfigItem item in GitCommandConfiguration.Default.Get(Command))
        {
            configuration.Add(item, Command);
        }

        if (!useRelativePathsSet)
        {
            configuration.Add(new GitConfigItem(UseRelativePathsKey, "true"), Command);
        }

        string relativePath = Path.GetRelativePath(workingDirectory, Directory).ToPosixPath().Quote();
        return new GitArgumentBuilder(Command, configuration)
        {
            "add", relativePath, CreateNewBranch ? $"-b {NewBranchName}" : SelectedBranch,
        };
    }

    private bool IsTargetFolderValid()
    {
        if (string.IsNullOrWhiteSpace(Directory))
        {
            return false;
        }

        try
        {
            DirectoryInfo directoryInfo = new(Directory);
            return !directoryInfo.Exists
                   || (!directoryInfo.EnumerateFiles().Any() && !directoryInfo.EnumerateDirectories().Any());
        }
        catch
        {
            return false;
        }
    }
}
