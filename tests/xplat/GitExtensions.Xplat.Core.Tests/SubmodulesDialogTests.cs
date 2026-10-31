using AwesomeAssertions;
using GitExtensions.Xplat.Core.Operations;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class SubmodulesDialogTests
{
    [TestCase("https://example.com/team/tools.git", "tools")]
    [TestCase("git@example.com:team/tools.git", "tools")]
    [TestCase("C:/repos/library", "library")]
    public void ProposedLocalPath_should_be_the_repositorys_name(string remotePath, string expected)
        => SubmodulesDialog.ProposedLocalPath(remotePath).Should().Be(expected);

    [TestCase("")]
    [TestCase(null)]
    public void ProposedLocalPath_should_be_null_without_a_name(string? remotePath)
        => SubmodulesDialog.ProposedLocalPath(remotePath).Should().BeNull();

    [TestCase("url", "path", true)]
    [TestCase("", "path", false)]
    [TestCase("url", "", false)]
    [TestCase(null, null, false)]
    public void CanAdd_should_need_both_paths(string? remotePath, string? localPath, bool expected)
        => SubmodulesDialog.CanAdd(remotePath, localPath).Should().Be(expected);

    [Test]
    public void AddArguments_should_be_upstreams_submodule_add()
    {
        SubmodulesDialog.AddArguments("https://example.com/tools.git", "tools", "main", force: true).ToString()
            .Should().Be("submodule add -f -b \"main\" \"https://example.com/tools.git\" \"tools\"");
        SubmodulesDialog.AddArguments("https://example.com/tools.git", "tools", null, force: false).ToString()
            .Should().Be("submodule add \"https://example.com/tools.git\" \"tools\"");
    }
}
