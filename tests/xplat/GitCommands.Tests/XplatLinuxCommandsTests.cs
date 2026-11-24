using AwesomeAssertions;
using CommonTestUtils;
using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using NSubstitute;
using NUnit.Framework;

namespace GitCommandsTests_Git;

/// <summary>
///  POSIX counterparts of the path arguments in <c>CommandsTests</c>, which use Windows paths. The commands quote and pass
///  paths through unchanged on Linux, so the expected argument strings are the same as upstream's. Also the
///  <c>TryRemoveWorktree</c> cases that pass Windows paths or rely on a Windows file lock.
/// </summary>
[Platform(Include = "Linux,MacOsX")]
// Mirrors: CommandsTests
internal sealed class XplatLinuxCommandsTests
{
    private static IEnumerable<TestCaseData> AddSubmoduleTestCases()
    {
        yield return new TestCaseData("", null);
        yield return new TestCaseData("-c protocol.file.allow=always ", Commands.GetAllowFileConfig());
    }

    [Test, TestCaseSource(nameof(AddSubmoduleTestCases))]
    public void AddSubmoduleCmd(string config, IEnumerable<GitConfigItem> configs)
    {
        Commands.AddSubmodule("/remote/path", "/local/path", "branch", force: true, configs).Arguments
            .Should().Be($"{config}submodule add -f -b \"branch\" \"/remote/path\" \"/local/path\"");
    }

    [Test]
    public void ApplyDiffPatchCmd()
    {
        Commands.ApplyDiffPatch(false, "/hello/world.patch", PathUtil.ToPosixPath).Arguments.Should().Be("apply \"/hello/world.patch\"");
        Commands.ApplyDiffPatch(true, "/hello/world.patch", PathUtil.ToPosixPath).Arguments.Should().Be("apply --ignore-whitespace \"/hello/world.patch\"");
    }

    [TestCase(false, false, "/hello/world.patch", "am --3way \"/hello/world.patch\"")]
    [TestCase(false, true, "/hello/world.patch", "am --3way --ignore-whitespace \"/hello/world.patch\"")]
    [TestCase(true, false, "/hello/world.patch", "am --3way --signoff \"/hello/world.patch\"")]
    [TestCase(true, true, "/hello/world.patch", "am --3way --signoff --ignore-whitespace \"/hello/world.patch\"")]
    [TestCase(true, true, null, "am --3way --signoff --ignore-whitespace")]
    public void ApplyMailboxPatchCmd(bool signOff, bool ignoreWhitespace, string? patchFile, string expected)
    {
        Commands.ApplyMailboxPatch(signOff, ignoreWhitespace, patchFile, PathUtil.ToPosixPath).Arguments.Should().Be(expected);
    }

    [TestCase(false, true, false, null, false, "/myrepo/.git/file", null, "merge --no-ff --squash -F \"/myrepo/.git/file\" --no-edit branch")]
    public void MergeBranchCmd(bool allowFastForward, bool squash, bool noCommit, string? strategy, bool allowUnrelatedHistories, string? mergeCommitFilePath, int? log, string expected)
    {
        Commands.MergeBranch("branch", allowFastForward, squash, noCommit, strategy!, allowUnrelatedHistories, mergeCommitFilePath, PathUtil.ToPosixPath, log).Arguments.Should().Be(expected);
    }

    [Test]
    public void PushTagCmd()
    {
        Commands.PushTag("/path", "tag", all: false).Arguments.Should().Be("push --progress \"/path\" tag tag");
        Commands.PushTag("/path/path", " tag ", all: false).Arguments.Should().Be("push --progress \"/path/path\" tag tag");
        Commands.PushTag("/path", "tag", all: true).Arguments.Should().Be("push --progress \"/path\" --tags");
    }

    [TestCase("/repos/feature", "worktree remove --force \"/repos/feature\"")]
    [TestCase("/my repos/work tree", "worktree remove --force \"/my repos/work tree\"")]
    public void TryRemoveWorktree_should_let_git_remove_the_worktree(string worktreePath, string expectedArguments)
    {
        MockExecutable executable = new();
        IGitModule module = CreateModule(executable);

        using (executable.StageOutput(expectedArguments, ""))
        {
            bool removed = module.TryRemoveWorktree(worktreePath, out string? errorMessage);

            removed.Should().BeTrue();
            errorMessage.Should().BeNull();
        }

        executable.Verify();
    }

    // Upstream holds a file open to make the deletion fail, which POSIX allows; a folder without write permission keeps
    // its entries instead.
    [Test]
    public void TryRemoveWorktree_should_report_both_errors_if_the_directory_cannot_be_deleted_either()
    {
        if (OperatingSystem.IsWindows() || Environment.UserName == "root")
        {
            Assert.Ignore("root deletes regardless of folder permissions");
            return;
        }

        MockExecutable executable = new();
        IGitModule module = CreateModule(executable);
        string worktreePath = Path.Combine(Path.GetTempPath(), $"ge-worktree-{Guid.NewGuid():N}");
        string lockedFolder = Path.Combine(worktreePath, "locked");
        Directory.CreateDirectory(lockedFolder);
        File.WriteAllText(Path.Combine(worktreePath, ".git"), "gitdir: elsewhere");
        File.WriteAllText(Path.Combine(lockedFolder, "locked.txt"), "");
        File.SetUnixFileMode(lockedFolder, UnixFileMode.UserRead | UnixFileMode.UserExecute);

        try
        {
            using (executable.StageOutput($"worktree remove --force \"{worktreePath}\"", "", exitCode: 128, error: "fatal: failed to delete"))
            {
                bool removed = module.TryRemoveWorktree(worktreePath, out string? errorMessage);

                removed.Should().BeFalse();
                errorMessage.Should().Contain("fatal: failed to delete");
                errorMessage.Should().Contain("locked.txt");
            }

            executable.Verify();
        }
        finally
        {
            File.SetUnixFileMode(lockedFolder, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            Directory.Delete(worktreePath, recursive: true);
        }
    }

    private static IGitModule CreateModule(MockExecutable executable)
    {
        IGitModule module = Substitute.For<IGitModule>();
        module.GitExecutable.Returns(executable);
        module.GetPathForGitExecution(Arg.Any<string?>())
            .Returns(callInfo => PathUtil.GetPathForGitExecution(callInfo.Arg<string?>(), wslDistro: null));
        return module;
    }
}
