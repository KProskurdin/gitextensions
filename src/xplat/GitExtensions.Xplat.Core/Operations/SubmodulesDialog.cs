using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Configurations;
using GitExtensions.Extensibility.Git;
using GitExtUtils;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  Upstream's <c>FormSubmodules</c> and <c>FormAddSubmodule</c> without their windows: the submodules and their details,
///  removing one from the configuration, and the command and suggestions of adding one.
/// </summary>
public static class SubmodulesDialog
{
    // Upstream's texts.
    public const string RemoveSelectedSubmodule = "Are you sure you want remove the selected submodule?";
    public const string RemoveSelectedSubmoduleCaption = "Remove";
    public const string RemoteAndLocalPathRequired = "A remote path and local path are required";

    /// <summary>
    ///  Upstream's <c>Initialize</c>: <c>git submodule status</c> with each submodule's .gitmodules data.
    /// </summary>
    public static IReadOnlyList<GitSubmoduleInfo> Load(IGitModule module)
        => [.. module.GetSubmodulesInfo().OfType<GitSubmoduleInfo>()];

    /// <summary>
    ///  Upstream's <c>RemoveSubmoduleClick</c> after its question: the submodule out of the index, its section out of
    ///  .gitmodules (staged, or the file unstaged when no submodule is left) and out of the local configuration. The work
    ///  tree folder stays, as upstream's.
    /// </summary>
    /// <exception cref="GitCommands.Config.GitConfigurationException">.gitmodules could not be read.</exception>
    public static void Remove(IGitModule module, string name, string localPath)
    {
        module.UnstageFile(localPath);

        ISubmodulesConfigFile submoduleConfigFile = module.GetSubmodulesConfigFile();
        submoduleConfigFile.RemoveConfigSection("submodule \"" + name + "\"");
        if (submoduleConfigFile.ConfigSections.Count > 0)
        {
            submoduleConfigFile.Save();
            module.StageFile(".gitmodules");
        }
        else
        {
            module.UnstageFile(".gitmodules");
        }

        module.RemoveConfigSection("submodule", name);
    }

    /// <summary>
    ///  Upstream's <c>AddClick</c> check: both the remote and the local path are needed.
    /// </summary>
    public static bool CanAdd(string? remotePath, string? localPath)
        => !string.IsNullOrEmpty(remotePath) && !string.IsNullOrEmpty(localPath);

    /// <summary>
    ///  The command of upstream's <c>AddClick</c>.
    /// </summary>
    public static ArgumentString AddArguments(string remotePath, string localPath, string? branch, bool force)
        => Commands.AddSubmodule(remotePath, localPath, branch ?? "", force);

    /// <summary>
    ///  Upstream's <c>DirectoryTextUpdate</c>: the repository's name proposed as the local path, or null when the remote
    ///  path has none.
    /// </summary>
    public static string? ProposedLocalPath(string? remotePath)
        => PathUtil.GetRepositoryName(remotePath) is { Length: > 0 } name ? name : null;

    /// <summary>
    ///  Upstream's <c>LoadRemoteRepoBranches</c>: the branches of the remote repository (<c>git ls-remote --heads</c>, which
    ///  needs no local clone); git's errors and warnings are ignored.
    /// </summary>
    public static IReadOnlyList<string> LoadRemoteBranches(IExecutable gitExecutable, string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return [];
        }

        GitArgumentBuilder arguments = new("ls-remote") { "--heads", url.ToPosixPath().Quote() };
        return
        [
            .. gitExecutable.GetOutput(arguments).LazySplit('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(head =>
                {
                    int branchIndex = head.IndexOf(GitRefName.RefsHeadsPrefix, StringComparison.Ordinal);
                    return branchIndex == -1 ? null : head[(branchIndex + GitRefName.RefsHeadsPrefix.Length)..];
                })
                .WhereNotNull()
        ];
    }
}
