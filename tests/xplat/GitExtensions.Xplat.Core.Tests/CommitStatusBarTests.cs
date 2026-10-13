using AwesomeAssertions;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class CommitStatusBarTests
{
    [TestCase("", 0, 1, 1)]
    [TestCase("subject", 7, 1, 8)]
    [TestCase("subject\n\nbody", 9, 3, 1)]
    [TestCase("subject\n\nbody", 11, 3, 3)]
    [TestCase("ab", 99, 1, 3)]
    public void Caret_should_give_the_one_based_line_and_column(string text, int caret, int line, int column)
    {
        CommitStatusBar.Caret(text, caret).Should().Be((line, column));
    }

    [Test]
    public void Staged_should_read_staged_of_all()
    {
        CommitStatusBar.Staged(1, 3).Should().Be("1/3");
    }

    [Test]
    public void BranchText_should_have_an_arrow_only_when_the_branch_pushes_somewhere()
    {
        new CommitBranchStatus("main", "origin/main").BranchText.Should().Be("main →");
        new CommitBranchStatus("(no branch)", PushTo: null).BranchText.Should().Be("(no branch)");
    }
}
