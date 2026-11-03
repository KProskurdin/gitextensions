using AwesomeAssertions;
using GitCommands;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Diff;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class RevisionFileStatusTests
{
    [TestCase("D", "FileStatusRemoved")]
    [TestCase("A", "FileStatusAdded")]
    [TestCase("?", "FileStatusAdded")]
    [TestCase("U", "Unmerged")]
    [TestCase("M", "FileStatusModified")]
    [TestCase("R", "FileStatusRenamed")]
    [TestCase("C", "FileStatusCopied")]
    [TestCase("X", "FileStatusUnknown")]
    public void ImageForStatus_should_be_upstreams_status_image(string status, string image)
    {
        FileStatusList.ImageForStatus(status).Should().Be(image);
        new CommitFile(status, "a.txt").Image.Should().Be(image);
    }

    [TestCase("A", DiffBranchStatus.OnlyBChange, "FileStatusAddedOnlyB")]
    [TestCase("D", DiffBranchStatus.OnlyAChange, "FileStatusRemovedOnlyA")]
    [TestCase("M", DiffBranchStatus.UnequalChange, "FileStatusModifiedUnequal")]
    [TestCase("R", DiffBranchStatus.SameChange, "FileStatusRenamedSame")]
    [TestCase("M", DiffBranchStatus.Unknown, "FileStatusModified")]
    [TestCase("U", DiffBranchStatus.OnlyAChange, "Unmerged")]
    public void ImageForStatus_should_add_the_side_of_a_BASE_groups_file(string status, DiffBranchStatus side, string image)
    {
        FileStatusList.ImageForStatus(status, side).Should().Be(image);
        new RevisionDiffFile(status, "a.txt", null, IsTracked: true) { DiffStatus = side }.Image.Should().Be(image);
    }

    [Test]
    public void Sort_should_group_commit_files_as_upstream()
    {
        CommitFile[] files = [new("A", "a.txt"), new("M", "z.md"), new("D", "m.cs"), new("U", "b.txt")];

        Paths(DiffListSortType.FilePath).Should().Equal("a.txt", "b.txt", "m.cs", "z.md");
        Paths(DiffListSortType.FileStatusFlat).Should().Equal("b.txt", "z.md", "a.txt", "m.cs");
        Paths(DiffListSortType.FileExtension).Should().Equal("m.cs", "z.md", "a.txt", "b.txt");
        return;

        IEnumerable<string> Paths(DiffListSortType sorting)
            => FileStatusList.Sort(files, sorting, file => file.Path, file => FileStatusList.KindOf(file.Status))
                .Select(file => file.Path);
    }
}
