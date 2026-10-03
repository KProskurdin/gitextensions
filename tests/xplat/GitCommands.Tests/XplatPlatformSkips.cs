using NUnit.Framework;
using NUnit.Framework.Interfaces;

// Marks upstream tests as ignored, with a reason, when they run on a platform they assert Windows semantics for.
// Ignored tests are listed in the test report, so the gap stays visible. The upstream test files are not edited;
// Linux-specific expectations live in XplatLinuxExpectations.cs.
[assembly: GitCommandsTests.XplatSkipOnNonWindows]

namespace GitCommandsTests;

[AttributeUsage(AttributeTargets.Assembly)]
public sealed class XplatSkipOnNonWindowsAttribute : Attribute, ITestAction
{
    private const string WindowsPathInput = "Windows path syntax as input (drive letters, backslashes, UNC); '\\' is an ordinary filename character on this OS";
    private const string WindowsOnlyFeature = "Windows-only feature (WSL, Cygwin, cmd.exe batch limits, CreateProcess errors)";
    private const string PlatformNativeExpectation = "Expects Windows line endings or Windows path separators; Linux behavior is covered in XplatLinuxExpectations";

    // Method-level so passing cases of the same method stay excluded too. Each entry needs a per-test decision before
    // it counts as Linux coverage.
    private static readonly (string Method, string Reason)[] _skips =
    [
        ("GitCommandsTests.CommitMessageManagerTests.WriteCommitMessageToFileAsync_no_bom", PlatformNativeExpectation),
        ("GitCommandsTests.DiffMergeTools.DiffMergeToolConfigurationManagerTests.LoadDiffMergeToolConfig_should_create_tool_config_with_userSuppliedPath_if_tool_unregistered", WindowsPathInput),
        ("GitCommandsTests.FullPathResolverTests.Resolve_combines_paths", WindowsPathInput),
        ("GitCommandsTests.FullPathResolverTests.Resolve_does_not_throw_on_invalid_workingDir", WindowsPathInput),
        ("GitCommandsTests.FullPathResolverTests.Resolve_handles_system_filenames", WindowsPathInput),
        ("GitCommandsTests.FullPathResolverTests.Resolve_should_return_full_path", WindowsPathInput),
        ("GitCommandsTests.FullPathResolverTests.Resolve_should_return_long_full_path", WindowsPathInput),
        ("GitCommandsTests.FullPathResolverTests.Resolve_should_return_original_path_if_rooted", WindowsPathInput),
        ("GitCommandsTests.Git.ExecutableExtensionsTests.RunBatchCommand_can_handle_max_length_arguments", WindowsOnlyFeature),
        ("GitCommandsTests.Git.ExecutableExtensionsTests.RunBatchCommand_throw_when_cmd_exceed_max_length", WindowsOnlyFeature),
        ("GitCommandsTests.Git.GitBranchNameNormaliserTest.Normalise_rule04", PlatformNativeExpectation),
        ("GitCommandsTests.Git.GitDirectoryResolverTests.Resolve_should_return_resolved_full_path_from_git_file_if_present", WindowsPathInput),
        ("GitCommandsTests.Git.GitDirectoryResolverTests.Resolve_submodule_real_filesystem", WindowsPathInput),
        ("GitCommandsTests.Git.GitExecutorTests.GitExecutable_for_wsl_working_dir_uses_exec_to_bypass_the_distro_shell", WindowsOnlyFeature),
        ("GitCommandsTests.Git.GitModuleWorktreeTests.GetWorktrees_should_handle_path_with_spaces", WindowsPathInput),
        ("GitCommandsTests.Git.GitModuleWorktreeTests.GetWorktrees_should_parse_multiple_worktrees", WindowsPathInput),
        ("GitCommandsTests.Git.GitModuleWorktreeTests.GetWorktrees_should_parse_single_worktree_with_branch", WindowsPathInput),
        ("GitCommandsTests.GitModuleTests.GetRemotes_should_parse_correctly_configured_remotes", WindowsPathInput),
        ("GitCommandsTests.GitModuleTests.GetTagMessage", PlatformNativeExpectation),
        ("GitCommandsTests.Helpers.PathUtilTest.FindAncestors", WindowsPathInput),
        ("GitCommandsTests.Helpers.PathUtilTest.GetDisplayPath", WindowsPathInput),
        ("GitCommandsTests.Helpers.PathUtilTest.GetPathForGitExecution_GetWindowsPath_default", WindowsPathInput),
        ("GitCommandsTests.Helpers.PathUtilTest.GetPathForGitExecution_unexpected_usage", WindowsPathInput),
        ("GitCommandsTests.Helpers.PathUtilTest.GetPathForGitExecution_wsl", WindowsOnlyFeature),
        ("GitCommandsTests.Helpers.PathUtilTest.GetWindowsPath_wsl", WindowsOnlyFeature),
        ("GitCommandsTests.Helpers.PathUtilTest.IsValidPathChar_should_return_expected", PlatformNativeExpectation),
        ("GitCommandsTests.Helpers.PathUtilTest.ToCygwinPathTest", WindowsOnlyFeature),
        ("GitCommandsTests.Helpers.PathUtilTest.ToMountPathTest", WindowsOnlyFeature),
        ("GitCommandsTests.Helpers.PathUtilTest.ToWslPathTest", WindowsOnlyFeature),
        ("GitCommandsTests.PathEqualityComparerTests.Equals", PlatformNativeExpectation),
        ("GitCommandsTests.UserRepositoryHistory.LocalRepositoryManagerTests.AddAsMostRecentAsync_should_add_new_path_as_top_entry", WindowsPathInput),
        ("GitCommandsTests.UserRepositoryHistory.LocalRepositoryManagerTests.AddAsMostRecentAsync_should_move_existing_path_as_top_entry", WindowsPathInput),
        ("GitCommandsTests.UserRepositoryHistory.LocalRepositoryManagerTests.AddAsMostRecentAsync_should_move_only_first_existing_path_as_top_entry", WindowsPathInput),
        ("GitCommandsTests.UserRepositoryHistory.LocalRepositoryManagerTests.AddAsMostRecentAsync_should_not_move_if_path_already_as_top_entry", WindowsPathInput),
        ("GitCommandsTests.UserRepositoryHistory.RecentRepoSplitterTests.SplitRecentRepos_Should_display_middle_dots_in_caption", WindowsPathInput),
        ("GitCommandsTests.UserRepositoryHistory.RecentRepoSplitterTests.SplitRecentRepos_Should_not_shorten_but_handle_user_folder_as_caption", WindowsPathInput),
        ("GitCommandsTests.UserRepositoryHistory.RecentRepoSplitterTests.SplitRecentRepos_Should_split_depending_anchor", WindowsPathInput),
        ("GitCommandsTests.UserRepositoryHistory.RecentRepoSplitterTests.SplitRecentRepos_Should_split_depending_anchor_and_sort_alphabetically", WindowsPathInput),
        ("GitCommandsTests.UserRepositoryHistory.RecentRepoSplitterTests.SplitRecentRepos_Should_split_depending_anchor_and_sort_alphabetically_Hiding_Top_Repo_In_Recent_list", WindowsPathInput),
        ("GitCommandsTests.UserRepositoryHistory.RecentRepoSplitterTests.SplitRecentRepos_Should_use_most_significant_folder_as_caption", WindowsPathInput),
        ("GitCommandsTests.UserRepositoryHistory.RepositoryDescriptionProviderTests.RepositoryDescriptionProvider_should_handle_subrepos", WindowsPathInput),
        ("GitCommandsTests.UserRepositoryHistory.RepositoryDescriptionProviderTests.RepositoryDescriptionProvider_should_not_skip_uninformative_root_repo_name", WindowsPathInput),
        ("GitCommandsTests.UserRepositoryHistory.RepositoryDescriptionProviderTests.RepositoryDescriptionProvider_should_not_skip_uninformative_submodule_name_to_parent_repo", WindowsPathInput),
        ("GitCommandsTests.UserRepositoryHistory.RepositoryDescriptionProviderTests.RepositoryDescriptionProvider_should_skip_uninformative_submodule_name", WindowsPathInput),
        ("GitCommandsTests_Git.CommandsTests.AddSubmoduleCmd", WindowsPathInput),
        ("GitCommandsTests_Git.CommandsTests.ApplyDiffPatchCmd", WindowsPathInput),
        ("GitCommandsTests_Git.CommandsTests.ApplyMailboxPatchCmd", WindowsPathInput),
        ("GitCommandsTests_Git.CommandsTests.MergeBranchCmd", WindowsPathInput),
        ("GitCommandsTests_Git.CommandsTests.PushTagCmd", WindowsPathInput),
    ];

    public ActionTargets Targets => ActionTargets.Test;

    public void BeforeTest(ITest test)
    {
        if (OperatingSystem.IsWindows() || test.Method is null)
        {
            return;
        }

        // FullName carries the parameter list; the method identity is everything before it.
        string method = test.FullName.Split('(')[0];
        foreach ((string skipped, string reason) in _skips)
        {
            if (method == skipped)
            {
                throw new IgnoreException($"Not run on this platform: {reason}.");
            }
        }
    }

    public void AfterTest(ITest test)
    {
    }
}
