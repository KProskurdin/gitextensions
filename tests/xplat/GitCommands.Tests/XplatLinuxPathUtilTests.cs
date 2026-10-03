using AwesomeAssertions;
using GitCommands;
using NUnit.Framework;

namespace GitCommandsTests.Helpers;

/// <summary>
///  POSIX counterparts of the Windows-path cases in <c>PathUtilTest</c>. WSL and Cygwin mapping stay Windows-only (see
///  XplatPlatformSkips.cs).
/// </summary>
[Platform(Include = "Linux,MacOsX")]
// Mirrors: PathUtilTest
internal sealed class XplatLinuxPathUtilTests
{
    // On POSIX '\' is an ordinary filename character, so only the characters the rule accepts on every platform differ.
    [TestCase('a', true)]
    [TestCase('/', true)]
    [TestCase('|', true)]
    [TestCase('\\', true)]
    [TestCase('"', false)]
    [TestCase('<', false)]
    [TestCase('>', false)]
    [TestCase(':', false)]
    [TestCase('^', false)]
    [TestCase(' ', false)]
    [TestCase('~', false)]
    [TestCase('\0', false)]
    [TestCase('\x7F', false)]
    public void IsValidPathChar_should_return_expected(char c, bool expected)
    {
        PathUtil.IsValidPathChar(c).Should().Be(expected);
    }

    [Test]
    public void GetDisplayPath()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        PathUtil.GetDisplayPath(Path.Combine(home, "SomePath")).Should().Be("~/SomePath");
        PathUtil.GetDisplayPath("/srv/SomePath").Should().Be("/srv/SomePath");
    }

    [TestCase("/foo/bar", new[] { "/foo/", "/" })]
    [TestCase("/foo/bar/", new[] { "/foo/", "/" })]
    [TestCase("/foo", new[] { "/" })]
    [TestCase("/foo/", new[] { "/" })]
    [TestCase("/", new string[0])]
    public void FindAncestors(string? path, string[] expected)
    {
        PathUtil.FindAncestors(path!).ToArray().Should().Equal(expected);
    }

    [TestCase("work/../GitExtensions/", "", "work/../GitExtensions/")]
    [TestCase("/srv/repo/", "", "/srv/repo/")]
    public void GetPathForGitExecution_default(string? path, string wslDistro, string expected)
    {
        PathUtil.GetPathForGitExecution(path, wslDistro).Should().Be(expected);
    }
}
