using AwesomeAssertions;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class CommitMessageFormatTests
{
    private static readonly string _newLine = Environment.NewLine;

    [TestCase("subject\nbody", true, "subject{0}{0}body{0}")]
    [TestCase("subject\nbody", false, "subject{0}body{0}")]
    [TestCase("subject\n\nbody", true, "subject{0}{0}body{0}")]
    [TestCase("subject", true, "subject{0}")]
    [TestCase("", true, "")]
    [TestCase("#123 fixed\nbody", false, "#123 fixed{0}body{0}")]
    public void Format_should_end_every_line_with_the_platform_newline_and_keep_the_second_line_empty_when_asked(
        string message, bool ensureSecondLineEmpty, string expected)
    {
        CommitMessageFormat.Format(message, ensureSecondLineEmpty).Should().Be(string.Format(expected, _newLine));
    }

    [Test]
    public void Format_should_take_the_text_boxs_windows_line_breaks_as_plain_ones()
    {
        CommitMessageFormat.Format("subject\r\nbody\r\nmore", ensureSecondLineEmpty: true).Should()
            .Be($"subject{_newLine}{_newLine}body{_newLine}more{_newLine}");
    }
}
