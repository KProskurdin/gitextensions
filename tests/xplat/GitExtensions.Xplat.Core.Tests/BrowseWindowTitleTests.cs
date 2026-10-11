using AwesomeAssertions;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class BrowseWindowTitleTests
{
    private string _root = "";
    private string _folder = "";

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "xplat-title-" + Guid.NewGuid().ToString("N"));
        _folder = Path.Combine(_root, "repo");
        Directory.CreateDirectory(_folder);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_root, recursive: true);

    [Test]
    public void Of_should_be_the_application_name_without_a_repository()
    {
        BrowseWindowTitle.Of(null, null, "no branch").Should().Be("Git Extensions");
    }

    [Test]
    public void Of_should_show_the_repository_and_its_branch()
    {
        BrowseWindowTitle.Of(_folder, "main", "no branch").Should().Be("repo (main) - Git Extensions");
    }

    [Test]
    public void Of_should_show_the_default_while_the_branch_is_not_known()
    {
        BrowseWindowTitle.Of(_folder, "", "no branch").Should().Be("repo (no branch) - Git Extensions");
    }

    [Test]
    public void Of_should_start_with_the_filtered_file()
    {
        BrowseWindowTitle.Of(_folder, "main", "no branch", "\"src/a.txt\"")
            .Should().Be("\"a.txt\" repo (main) - Git Extensions");
    }
}
