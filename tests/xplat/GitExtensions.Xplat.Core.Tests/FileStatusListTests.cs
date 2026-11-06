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
    public void FileMenuState_should_offer_the_index_flags_for_tracked_working_directory_files_as_upstream()
    {
        FileChange edited = new("a.txt", ChangeKind.Modified, Staged: false) { IsSkipWorktree = true };
        FileChange untracked = new("b.txt", ChangeKind.Untracked, Staged: false);

        FileMenuState state = FileMenuState.For([edited], staged: false, _ => true);
        (state.IndexFlags, state.SkipWorktreeChecked, state.AssumeUnchangedChecked).Should().Be((true, true, false));
        FileMenuState.For([untracked], staged: false, _ => true).IndexFlags.Should().BeFalse("git flags only tracked files");
        FileMenuState.For([edited with { Staged = true }], staged: true, _ => true).IndexFlags
            .Should().BeFalse("upstream offers them for the working directory's files");
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

    [Test]
    public void FileMenuState_should_follow_upstreams_rules_for_each_list()
    {
        FileChange untracked = new("new.md", ChangeKind.Untracked, Staged: false);
        FileChange modified = new("a.txt", ChangeKind.Modified, Staged: true);

        FileMenuState unstaged = FileMenuState.For([untracked], staged: false, _ => true);
        (unstaged.Stage, unstaged.Unstage, unstaged.ResetFile, unstaged.Ignore, unstaged.History, unstaged.StopTracking)
            .Should().Be((true, false, false, true, false, false));
        (unstaged.OpenWorkingFile, unstaged.DeleteFile).Should().Be((true, true));

        FileMenuState staged = FileMenuState.For([modified], staged: true, _ => false);
        (staged.Stage, staged.Unstage, staged.ResetFile, staged.Ignore, staged.History, staged.StopTracking)
            .Should().Be((false, true, true, false, true, true));
        (staged.OpenWorkingFile, staged.DeleteFile).Should().Be((false, false), "the file is not on disk");

        FileMenuState none = FileMenuState.For([], staged: false, _ => true);
        (none.Stage, none.CopyPaths, none.DeleteFile).Should().Be((false, false, false));
    }
}
