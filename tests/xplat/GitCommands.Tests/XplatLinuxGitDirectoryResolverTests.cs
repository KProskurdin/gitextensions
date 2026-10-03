using System.IO.Abstractions;
using AwesomeAssertions;
using CommonTestUtils;
using GitCommands;
using GitCommands.Git;
using NSubstitute;
using NUnit.Framework;

namespace GitCommandsTests.Git;

/// <summary>
///  POSIX counterparts of the <c>GitDirectoryResolverTests</c> cases that build Windows paths.
/// </summary>
[Platform(Include = "Linux,MacOsX")]
// Mirrors: GitDirectoryResolverTests
internal sealed class XplatLinuxGitDirectoryResolverTests
{
    private const string WorkingDir = "/dev/repo";
    private readonly string _gitFile = Path.Combine(WorkingDir, ".git");
    private FileBase _file = null!;
    private DirectoryBase _directory = null!;
    private GitDirectoryResolver _resolver = null!;

    [SetUp]
    public void Setup()
    {
        _file = Substitute.For<FileBase>();
        _directory = Substitute.For<DirectoryBase>();
        IFileSystem fileSystem = Substitute.For<IFileSystem>();
        fileSystem.Directory.Returns(_directory);
        fileSystem.File.Returns(_file);

        _directory.Exists(WorkingDir).Returns(true);

        _resolver = new GitDirectoryResolver(fileSystem);
    }

    [Test]
    public void Resolve_should_return_resolved_full_path_from_git_file_if_present()
    {
        _file.Exists(_gitFile).Returns(true);
        _file.ReadLines(_gitFile).Returns(new[] { "", " ", "gitdir: ../.git/modules/Externals/Git.hub", "text" });

        _resolver.Resolve(WorkingDir).Should().Be("/dev/.git/modules/Externals/Git.hub/");

        _directory.DidNotReceive().Exists(Path.Combine(WorkingDir, ".git").EnsureTrailingPathSeparator());
    }

    [Test]
    public void Resolve_submodule_real_filesystem()
    {
        using GitModuleTestHelper helper = new();
        string submodulePath = Path.Combine(helper.Module.WorkingDir, "External", "Git.hub");
        helper.CreateFile(submodulePath, ".git", "\r \r\ngitdir: ../../.git/modules/Externals/Git.hub\r\ntext");
        _resolver = new GitDirectoryResolver();

        _resolver.Resolve(submodulePath).Should().Be(Path.Combine(helper.Module.WorkingDirGitDir, "modules", "Externals", "Git.hub") + Path.DirectorySeparatorChar);
        _resolver.Resolve(helper.Module.WorkingDir).Should().Be(helper.Module.WorkingDirGitDir);
    }
}
