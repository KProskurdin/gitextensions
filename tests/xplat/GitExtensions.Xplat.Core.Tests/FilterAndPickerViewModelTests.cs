using System.Text;
using AwesomeAssertions;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using NSubstitute;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class FilterAndPickerViewModelTests
{
    private const string RepositoryPath = "/work/repo";

    private static readonly string _first = new('1', 40);
    private static readonly string _second = new('2', 40);
    private static readonly string _third = new('3', 40);

    private string _folder = null!;

    [SetUp]
    public void SetUp()
        => _folder = Directory
            .CreateDirectory(Path.Combine(Path.GetTempPath(), "xplat-vm-" + Guid.NewGuid().ToString("N")))
            .FullName;

    [TearDown]
    public void TearDown() => Directory.Delete(_folder, recursive: true);

    [Test]
    public void FilterViewModel_should_enable_as_upstream_and_return_only_the_filters_turned_on()
    {
        FakeDialogHost host = new();
        FilterViewModel viewModel = new(host, new RevisionFilter(Author: "ann", CommitsLimit: 0), defaultLimit: 500);

        viewModel.HasAuthor.Should().BeTrue();
        viewModel.CanIgnoreCase.Should().BeTrue("a text filter is on");
        viewModel.CommitsLimit.Should().Be(500, "the default limit shows while none is set");
        viewModel.ShowReflog = true;
        viewModel.CanShowCurrentBranchOnly.Should().BeFalse();
        viewModel.CanFilterBranches.Should().BeFalse();
        viewModel.ShowReflog = false;
        viewModel.HasCommitsLimit = true;
        viewModel.CommitsLimit = 7;
        viewModel.HasMessage = false;
        viewModel.Message = "ignored while its check box is off";
        viewModel.HasUntil = true;
        viewModel.Until = new DateTime(2026, 11, 14);
        viewModel.ApplyCommand.Execute(null);

        RevisionFilter filter = (RevisionFilter)host.Result!;
        (filter.Author, filter.Message, filter.CommitsLimit, filter.Until).Should().Be(
            ("ann", "", 7, new DateTime(2026, 11, 14, 23, 59, 59)));
        viewModel.HasCommitsLimit = false;
        viewModel.CommitsLimit.Should().Be(500, "an unchecked limit shows the default again");
    }

    [Test]
    public async Task AddToGitIgnoreViewModel_should_append_the_patterns_and_close()
    {
        FakeDialogHost host = new();
        AddToGitIgnoreViewModel viewModel = new(host, _folder, localExclude: false, ["*.log", "", "bin/"]);

        await viewModel.AddToIgnoreCommand.ExecuteAsync(null);

        viewModel.Added.Should().BeTrue();
        File.ReadAllText(Path.Combine(_folder, ".gitignore")).Should().Contain("*.log").And.Contain("bin/");
        host.IsClosed.Should().BeTrue();
        viewModel.Title.Should().Be(FakeDialogHost.TranslatedMark + "Add file(s) to .gitignore");
    }

    [Test]
    public void ViewPatchViewModel_should_list_the_file_patches_and_sort_by_a_column_both_ways()
    {
        string path = Path.Combine(_folder, "change.patch");
        File.WriteAllText(path, string.Join("\n",
            "From 1111111111111111111111111111111111111111 Mon Sep 17 00:00:00 2001",
            "Subject: [PATCH] change",
            "---",
            "diff --git a/b.txt b/b.txt",
            "index 1111111..2222222 100644",
            "--- a/b.txt",
            "+++ b/b.txt",
            "@@ -1 +1 @@",
            "-one",
            "+two",
            "diff --git a/a.txt b/a.txt",
            "index 1111111..2222222 100644",
            "--- a/a.txt",
            "+++ b/a.txt",
            "@@ -1 +1 @@",
            "-one",
            "+two",
            ""));
        IGitModule module = Substitute.For<IGitModule>();
        module.FilesEncoding.Returns(Encoding.UTF8);
        ViewPatchViewModel viewModel = new(new FakeDialogHost(), module, path);
        viewModel.Patches.Select(patch => patch.FileNameB).Should().Equal("b.txt", "a.txt");

        viewModel.SortByCommand.Execute(0);
        viewModel.Patches.Select(patch => patch.FileNameB).Should().Equal("a.txt", "b.txt");
        viewModel.SortByCommand.Execute(0);
        viewModel.Patches.Select(patch => patch.FileNameB).Should().Equal("b.txt", "a.txt");
    }

    [Test]
    public async Task ChooseCommitViewModel_should_select_the_first_commit_link_its_parents_and_choose_it()
    {
        FakeCommitHistory history = new();
        FakeDialogHost host = new();
        ChooseCommitViewModel viewModel = new(host, Substitute.For<IGitModule>(), RepositoryPath, history,
            preselect: null);

        Task load = viewModel.LoadAsync();
        history.CompletePage(0, new CommitPage(
        [
            new CommitRow(_third, "333", "third", "a", "d", ParentHashes: [_second, _first]),
            new CommitRow(_second, "222", "second", "a", "d", ParentHashes: [_first]),
        ], HasMore: false));
        await load;

        viewModel.SelectedIndex.Should().Be(0);
        viewModel.HasParents.Should().BeTrue();
        (viewModel.Parent1, viewModel.Parent2Text).Should().Be((_second, ObjectId.Parse(_first).ToShortString()));
        viewModel.SelectOrTellCommand.Execute(_second);
        viewModel.SelectedIndex.Should().Be(1);
        viewModel.SelectOrTellCommand.Execute(_first);
        host.Errors.Should().ContainSingle("the root is not listed");
        viewModel.ChooseCommand.Execute(null);
        ((CommitRow)host.Result!).Subject.Should().Be("second");
    }
}
