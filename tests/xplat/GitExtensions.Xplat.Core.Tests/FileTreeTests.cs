using AwesomeAssertions;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Diff;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class FileTreeTests
{
    [Test]
    public void Build_should_nest_files_in_folders_with_folders_first()
    {
        IReadOnlyList<FileTreeNode> tree = FileTree.Build(["readme.md", "src/b.cs", "src/a/x.cs", "Assets/logo.png", "src/A.txt"]);

        tree.Select(node => (node.Name, node.IsFolder)).Should().Equal(("Assets", true), ("src", true), ("readme.md", false));
        FileTreeNode src = tree[1];
        src.Children.Select(node => node.Name).Should().Equal("a", "A.txt", "b.cs");
        src.Children[0].Children.Should().ContainSingle().Which.Path.Should().Be("src/a/x.cs");
        src.Path.Should().Be("src");
    }

    [Test]
    public void Build_should_return_nothing_for_no_files()
    {
        FileTree.Build([]).Should().BeEmpty();
    }

    [Test]
    public void ParseText_should_number_each_line_and_ignore_the_last_line_end()
    {
        IReadOnlyList<DiffLine> lines = DiffParser.ParseText("first\r\nsecond\n");

        lines.Should().Equal(
            new DiffLine("first", DiffLineKind.Context, NewNumber: 1),
            new DiffLine("second", DiffLineKind.Context, NewNumber: 2));
    }
}
