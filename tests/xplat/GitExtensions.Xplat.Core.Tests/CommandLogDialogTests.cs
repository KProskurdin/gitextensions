using AwesomeAssertions;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class CommandLogDialogTests
{
    [TestCase(0, -1, 3, 2)]
    [TestCase(3, 2, 5, 4)]
    [TestCase(3, 0, 5, 0)]
    [TestCase(5, 3, 2, 1)]
    [TestCase(3, -1, 5, 4)]
    [TestCase(3, 1, 0, -1)]
    public void SelectionAfterRefresh_should_follow_the_last_row_or_keep_the_index(int oldCount, int oldSelected,
        int newCount, int expected)
    {
        CommandLogDialog.SelectionAfterRefresh(oldCount, oldSelected, newCount).Should().Be(expected);
    }

    [Test]
    public void PrintableChars_should_show_the_invisible_characters()
    {
        CommandLogDialog.PrintableChars("a b\tc\r\n\0\u001b").Should().Be("a·b»c\\r\\n\n\\0\\x1b");
    }

    [Test]
    public void PrintableChars_should_keep_null()
    {
        CommandLogDialog.PrintableChars(null).Should().BeNull();
    }

    [Test]
    public void CacheDetail_should_separate_the_command_output_and_error()
    {
        CommandLogDialog.CacheDetail("rev-parse HEAD", "abc\n", "")
            .Should().Be("rev-parse HEAD\n-------------------------------------\n\nabc\\n\n"
                         + "\n-------------------------------------\n\n");
    }

    [Test]
    public void CacheDisplay_should_leave_out_the_configuration()
    {
        CommandLogDialog.CacheDisplay("-c log.showSignature=false log -1").Should().Be("log -1");
    }
}
