using AwesomeAssertions;
using GitCommands;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class FileStatusListTests
{
    private static readonly FileChange[] _files =
    [
        new("src/b.txt", ChangeKind.Added, Staged: false),
        new("a.cs", ChangeKind.Deleted, Staged: false),
        new("src/a.txt", ChangeKind.Modified, Staged: false),
        new("c.cs", ChangeKind.Conflict, Staged: false),
        new("new.md", ChangeKind.Untracked, Staged: false),
    ];

    [TestCase(ChangeKind.Modified, "FileStatusModified")]
    [TestCase(ChangeKind.Added, "FileStatusAdded")]
    [TestCase(ChangeKind.Untracked, "FileStatusAdded")]
    [TestCase(ChangeKind.Deleted, "FileStatusRemoved")]
    [TestCase(ChangeKind.Renamed, "FileStatusRenamed")]
    [TestCase(ChangeKind.Conflict, "Unmerged")]
    public void ImageFor_should_give_upstreams_status_image(ChangeKind kind, string image)
    {
        FileStatusList.ImageFor(kind).Should().Be(image);
    }

    [Test]
    public void Sort_should_order_by_path_extension_or_status_as_upstreams_group_by()
    {
        FileStatusList.Sort(_files, DiffListSortType.FilePath).Select(file => file.Path)
            .Should().Equal("a.cs", "c.cs", "new.md", "src/a.txt", "src/b.txt");
        FileStatusList.Sort(_files, DiffListSortType.FileExtensionFlat).Select(file => file.Path)
            .Should().Equal("a.cs", "c.cs", "new.md", "src/a.txt", "src/b.txt");
        FileStatusList.Sort(_files, DiffListSortType.FileStatus).Select(file => file.Path)
            .Should().Equal("c.cs", "src/a.txt", "new.md", "src/b.txt", "a.cs");
    }

    [Test]
    public void FolderDescription_should_count_and_list_the_files_below_the_folder_as_upstream()
    {
        string nl = Environment.NewLine;

        FileStatusList.FolderDescription("src", ["src/a.cs", "src/sub/b.cs"])
            .Should().Be(("src/", $"(2) src/{nl}{nl}a.cs{nl}sub/b.cs"));
        FileStatusList.FolderDescription("", ["a.cs", "src/b.cs"])
            .Should().Be(("/", $"(2) /{nl}{nl}a.cs{nl}src/b.cs"), "a group node is the root folder");
    }

    [Test]
    public void Filter_should_be_a_case_insensitive_regex_and_report_an_invalid_one()
    {
        FileStatusList.Filter("", out _).Should().BeNull();
        FileStatusList.Filter(@"^SRC/.*\.TXT$", out string? error)!.IsMatch("src/a.txt").Should().BeTrue();
        error.Should().BeNull();

        FileStatusList.Filter("(", out error).Should().BeNull();
        error.Should().NotBeNullOrEmpty();
    }
}
