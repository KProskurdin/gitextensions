using AwesomeAssertions;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class GitIgnoreFileTests
{
    [TestCase("notes.txt", "/notes.txt")]
    [TestCase("logs/a.log", "/logs/a.log")]
    [TestCase("logs\\a.log", "/logs/a.log")]
    public void PatternFor_should_anchor_the_file_at_the_root(string path, string expected)
    {
        GitIgnoreFile.PatternFor(path).Should().Be(expected);
    }
}
