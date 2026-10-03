using AwesomeAssertions;
using CommonTestUtils;
using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using NUnit.Framework;

namespace GitCommandsTests.Git;

/// <summary>
///  POSIX counterparts of the worktree and remote path cases in <c>GitModuleWorktreeTests</c> and <c>GitModuleTests_Remotes</c>.
///  Git on Linux reports POSIX paths, which are returned as-is; upstream's Windows expectations convert 'C:/' to 'C:\'.
/// </summary>
[Platform(Include = "Linux,MacOsX")]
// Mirrors: GitModuleWorktreeTests
internal sealed class XplatLinuxGitModuleWorktreeTests
{
    private GitModule _gitModule = null!;
    private MockExecutable _executable = null!;

    [SetUp]
    public void SetUp()
    {
        GitVersion.ResetVersion();
        _executable = new MockExecutable();
        _executable.StageOutput("--version", $"git version {GitVersion.LastRecommendedVersion}");
        _gitModule = GetGitModuleWithExecutable(_executable);
    }

    [TearDown]
    public void TearDown()
    {
        _executable.Verify();
    }

    [Test]
    public void GetWorktrees_should_parse_single_worktree_with_branch()
    {
        string output = string.Join('\0', "worktree /repos/main", "HEAD abc1234abc1234abc1234abc1234abc1234abc12", "branch refs/heads/master", "", "");

        using (_executable.StageOutput("worktree list --porcelain -z", output))
        {
            IReadOnlyList<GitWorktree> worktrees = _gitModule.GetWorktrees();

            worktrees.Should().HaveCount(1);
            worktrees[0].Path.Should().Be("/repos/main");
            worktrees[0].Branch.Should().Be("master");
        }
    }

    [Test]
    public void GetWorktrees_should_parse_multiple_worktrees()
    {
        string output = string.Join('\0',
            "worktree /repos/main",
            "HEAD aaaa1234aaaa1234aaaa1234aaaa1234aaaa1234",
            "branch refs/heads/master",
            "",
            "worktree /repos/feature",
            "HEAD bbbb5678bbbb5678bbbb5678bbbb5678bbbb5678",
            "branch refs/heads/feature/my-feature",
            "", "");

        using (_executable.StageOutput("worktree list --porcelain -z", output))
        {
            IReadOnlyList<GitWorktree> worktrees = _gitModule.GetWorktrees();

            worktrees.Should().HaveCount(2);
            worktrees[0].Path.Should().Be("/repos/main");
            worktrees[0].Branch.Should().Be("master");
            worktrees[1].Path.Should().Be("/repos/feature");
            worktrees[1].Branch.Should().Be("feature/my-feature");
        }
    }

    [Test]
    public void GetWorktrees_should_handle_path_with_spaces()
    {
        string output = string.Join('\0', "worktree /my repos/work tree", "HEAD abc1234abc1234abc1234abc1234abc1234abc12", "branch refs/heads/main", "", "");

        using (_executable.StageOutput("worktree list --porcelain -z", output))
        {
            IReadOnlyList<GitWorktree> worktrees = _gitModule.GetWorktrees();

            worktrees.Should().HaveCount(1);
            worktrees[0].Path.Should().Be("/my repos/work tree");
            worktrees[0].Branch.Should().Be("main");
        }
    }

    private static GitModule GetGitModuleWithExecutable(IExecutable executable)
    {
        GitModule module = new(new GitExecutorProvider(new GitDirectoryResolver()), "");

        GitExecutor.TestAccessor executorAccessor = module.GetTestAccessor().Executor;
        executorAccessor.GitExecutable = executable;
        executorAccessor.GitWindowsExecutable = executable;
        executorAccessor.GitCommandRunner = new GitCommandRunner(executable, () => GitModule.SystemEncoding);

        return module;
    }
}
