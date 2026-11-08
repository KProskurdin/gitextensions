using AwesomeAssertions;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Platform;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

/// <summary>
///  Links are created on POSIX only: on Windows they need a privilege, and <see cref="RealPath"/> leaves them alone there.
/// </summary>
[Platform(Include = "Linux,MacOsX")]
internal sealed class RealPathTests
{
    private string _root = "";
    private string _real = "";
    private string _link = "";

    [SetUp]
    public void Setup()
    {
        _root = Directory.CreateTempSubdirectory("xplat-realpath-").FullName;
        _real = Directory.CreateDirectory(Path.Combine(_root, "real", "repo")).FullName;
        _link = Path.Combine(_root, "link");
        Directory.CreateSymbolicLink(_link, Path.Combine(_root, "real"));
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_root, recursive: true);

    [Test]
    public void Resolve_should_follow_a_link_in_the_middle_of_the_path()
    {
        RealPath.Resolve(Path.Combine(_link, "repo")).Should().Be(RealPath.Resolve(_real));
    }

    [Test]
    public void Resolve_should_follow_a_link_to_a_link()
    {
        string second = Path.Combine(_root, "second");
        Directory.CreateSymbolicLink(second, _link);

        RealPath.Resolve(Path.Combine(second, "repo")).Should().Be(RealPath.Resolve(_real));
    }

    [Test]
    public void Resolve_should_keep_a_part_that_does_not_exist()
    {
        RealPath.Resolve(Path.Combine(_link, "gone", "repo")).Should().Be(Path.Combine(RealPath.Resolve(_root), "real", "gone", "repo"));
    }

    [Test]
    public void IsCurrent_should_recognize_a_repository_opened_through_a_link()
    {
        WorktreeInfo worktree = new(RealPath.Resolve(_real), "repo", IsMain: false, IsDeleted: false);

        ManageWorktreeDialog.IsCurrent(worktree, Path.Combine(_link, "repo")).Should().BeTrue();
    }

    [Test]
    public void CanDelete_should_refuse_the_worktree_open_through_a_link()
    {
        WorktreeInfo main = new(Path.Combine(_root, "main"), "main", IsMain: true, IsDeleted: false);
        WorktreeInfo other = new(RealPath.Resolve(_real), "repo", IsMain: false, IsDeleted: false);

        ManageWorktreeDialog.CanDelete([main, other], 1, Path.Combine(_link, "repo")).Should().BeFalse();
    }
}
