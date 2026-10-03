using AwesomeAssertions;
using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using NUnit.Framework;

namespace GitCommandsTests_Git;

/// <summary>
///  POSIX counterparts of the path arguments in <c>CommandsTests</c>, which use Windows paths. The commands quote and pass
///  paths through unchanged on Linux, so the expected argument strings are the same as upstream's.
/// </summary>
[Platform(Include = "Linux,MacOsX")]
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
}
