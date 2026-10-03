using AwesomeAssertions;
using GitCommands;
using NUnit.Framework;

namespace GitCommandsTests;

/// <summary>
///  POSIX counterparts of <c>FullPathResolverTests</c>, which use Windows working directories. Same structure and expectations.
/// </summary>
[Platform(Include = "Linux,MacOsX")]
internal sealed class XplatLinuxFullPathResolverTests
{
    private const string WorkingDir = "/dev/repo";
    private FullPathResolver _resolver = null!;

    [SetUp]
    public void Setup()
    {
        _resolver = new FullPathResolver(() => WorkingDir);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public void Resolve_should_return_null_if_path_null_or_illegal_chars(string? path)
    {
        _resolver.Resolve(path).Should().BeNull();
    }

    [TestCase("/")]
    public void Resolve_should_return_original_path_if_rooted(string? path)
    {
        _resolver.Resolve(path).Should().Be(path);
    }

    [TestCase("folder/", 10000)]
    public void Resolve_should_return_long_full_path(string dir, int repeats)
    {
        string path = string.Concat(Enumerable.Repeat(dir, repeats)) + "filename.txt";
        _resolver.Resolve(path).Should().Be($"{WorkingDir}/{path}");
    }

    [TestCase("file")]
    [TestCase("folder/folder/folder/folder/folder/folder/folder/folder/folder/filename.txt")]
    [TestCase("folder/folder/folder/folder/folder/folder/folder/folder/folder#/filename.txt")]
    public void Resolve_should_return_full_path(string? path)
    {
        _resolver.Resolve(path).Should().Be($"{WorkingDir}/{path}");
    }

    [TestCase("drivers/gpu/drm/nouveau/nvkm/subdev/i2c/aux.c")]
    public void Resolve_handles_system_filenames(string? path)
    {
        _resolver.Resolve(path).Should().Be($"{WorkingDir}/{path}");
    }

    [TestCase("/dev/repo")]
    [TestCase("/dev/repo/")]
    [TestCase("/dev/c#/repo/")]
    public void Resolve_combines_paths(string? workingDir)
    {
        FullPathResolver resolver = new(() => workingDir!);
        resolver.Resolve("file.txt").Should().Be(Path.Combine(workingDir!, "file.txt"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public void Resolve_does_not_throw_on_invalid_workingDir(string? workingDir)
    {
        FullPathResolver resolver = new(() => workingDir!);
        resolver.Resolve("file.txt").Should().Be(Path.Combine(Environment.CurrentDirectory, "file.txt"));
    }
}
