using AwesomeAssertions;
using GitCommands;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class FileStatusTreeTests
{
    private static readonly string[] _paths = ["z.txt", "src/b.cs", "src/a.cs", "src/lib/c.cs", "docs/x/y/readme.md"];

    [Test]
    public void Build_should_put_the_folders_before_the_files_and_compact_a_folder_holding_one_folder()
    {
        IReadOnlyList<FileStatusTreeNode<File>> nodes = Build(DiffListSortType.FilePath);

        Describe(nodes).Should().Equal(
            "Folder docs/x/y", "  File readme.md",
            "Folder src", "  Folder lib", "    File c.cs", "  File a.cs", "  File b.cs",
            "File z.txt");
    }

    [Test]
    public void A_folder_node_should_know_its_path_files_and_node_keys()
    {
        FileStatusTreeNode<File> src = Build(DiffListSortType.FilePath).Single(node => node.Text == "src");

        src.FolderPath.Should().Be("src");
        src.Files().Select(file => file.Path).Should().Equal("src/lib/c.cs", "src/a.cs", "src/b.cs");
        src.NodeKeys().Should().Equal("src/", "src/lib/");
        src.Children[1].NodeKeys().Should().BeEmpty("a file node has none");
    }

    [Test]
    public void Build_should_show_a_folder_holding_one_file_as_the_file_in_a_dense_tree()
    {
        Describe(Build(DiffListSortType.FilePath, denseTree: true)).Should().Equal(
            "File docs/x/y/readme.md",
            "Folder src", "  File lib/c.cs", "  File a.cs", "  File b.cs",
            "File z.txt");
    }

    [Test]
    public void Build_should_list_the_paths_without_folders_in_a_flat_variant()
    {
        Describe(Build(DiffListSortType.FilePathFlat)).Should().Equal(
            "File docs/x/y/readme.md", "File src/lib/c.cs", "File src/a.cs", "File src/b.cs", "File z.txt");
    }

    [Test]
    public void Build_should_group_by_extension_with_a_tree_below_each_group_and_a_lone_file_without_its_group()
    {
        Describe(Build(DiffListSortType.FileExtension)).Should().Equal(
            "Group .cs", "  Folder src", "    Folder lib", "      File c.cs", "    File a.cs", "    File b.cs",
            "Group .md", "  Folder docs/x/y", "    File readme.md",
            "File z.txt");
        Describe(Build(DiffListSortType.FileExtensionFlat)).Should().Equal(
            ["File src/lib/c.cs", "File src/a.cs", "File src/b.cs", "File docs/x/y/readme.md", "File z.txt"],
            "a flat list drops the group nodes");
        Describe(Build(DiffListSortType.FileExtensionFlat, showGroupNodesInFlatList: true)).Should().StartWith(
            ["Group .cs", "  File src/lib/c.cs"]);
    }

    [Test]
    public void Build_should_show_a_single_group_as_the_list_itself()
    {
        Describe(FileStatusTree.Build([new File("a/x.cs", "M"), new File("b.cs", "M")], DiffListSortType.FileStatus,
                denseTree: false, showGroupNodesInFlatList: false, file => file.Path, file => file.Status,
                _ => ChangeKind.Modified))
            .Should().Equal("Folder a", "  File x.cs", "File b.cs");
    }

    private static IReadOnlyList<FileStatusTreeNode<File>> Build(DiffListSortType sorting, bool denseTree = false,
        bool showGroupNodesInFlatList = false)
        => FileStatusTree.Build(_paths.Select(path => new File(path, "M")), sorting, denseTree,
            showGroupNodesInFlatList,
            file => file.Path, file => file.Status, _ => ChangeKind.Modified);

    private static List<string> Describe(IReadOnlyList<FileStatusTreeNode<File>> nodes, int depth = 0)
        =>
        [
            .. nodes.SelectMany(node => (IEnumerable<string>)
            [
                $"{new string(' ', depth * 2)}{node.Kind} {node.Text}",
                .. Describe(node.Children, depth + 1),
            ])
        ];

    private sealed record File(string Path, string Status);
}
