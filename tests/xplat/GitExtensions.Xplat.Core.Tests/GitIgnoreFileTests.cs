using AwesomeAssertions;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class GitIgnoreFileTests
{
    [Test]
    public void AddPatterns_should_append_new_patterns_on_their_own_lines()
    {
        GitIgnoreFile.AddPatterns("bin/\nobj/", ["/notes.txt", "obj/", " /logs/a.log "])
            .Should().Be("bin/\nobj/\n/notes.txt\n/logs/a.log\n");
    }

    [Test]
    public void AddPatterns_should_keep_the_file_line_ending()
    {
        GitIgnoreFile.AddPatterns("bin/\r\n", ["/x"]).Should().Be("bin/\r\n/x\r\n");
    }

    [Test]
    public void AddPatterns_should_return_the_text_unchanged_when_every_pattern_is_there()
    {
        GitIgnoreFile.AddPatterns("/x\n", ["/x"]).Should().Be("/x\n");
    }

    [TestCase("notes.txt", "/notes.txt")]
    [TestCase("logs/a.log", "/logs/a.log")]
    [TestCase("logs\\a.log", "/logs/a.log")]
    public void PatternFor_should_anchor_the_file_at_the_root(string path, string expected)
    {
        GitIgnoreFile.PatternFor(path).Should().Be(expected);
    }
}
