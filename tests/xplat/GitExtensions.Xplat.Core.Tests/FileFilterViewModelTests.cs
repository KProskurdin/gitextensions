using AwesomeAssertions;
using GitCommands;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class FileFilterViewModelTests
{
    private InMemoryAppPreferences _preferences = null!;
    private int _changes;

    [SetUp]
    public void Setup()
    {
        _preferences = new InMemoryAppPreferences();
        _changes = 0;
    }

    [Test]
    public async Task Typing_should_filter_once_the_typing_stops()
    {
        FileFilterViewModel viewModel = NewViewModel(TimeSpan.FromMilliseconds(50));

        viewModel.Text = "\\.m";
        viewModel.Text = "\\.md$";

        viewModel.HasText.Should().BeTrue("the delete button shows at once");
        viewModel.Filter.Should().BeNull();
        await WaitUntilAsync(() => viewModel.Filter is not null);
        _changes.Should().Be(1, "only the last text typed is applied");
        viewModel.State.Should().Be(FileFilterState.Valid);
        viewModel.IsMatch("docs/readme.MD").Should().BeTrue("case is ignored");
        viewModel.IsMatch("a.txt").Should().BeFalse();
    }

    [Test]
    public void SetFilter_should_apply_at_once_and_an_invalid_expression_should_filter_nothing()
    {
        FileFilterViewModel viewModel = NewViewModel();

        viewModel.SetFilter("(");

        viewModel.Filter.Should().BeNull();
        viewModel.Error.Should().NotBeNull();
        viewModel.ErrorTip.Should().StartWith("RegEx: ");
        viewModel.State.Should().Be(FileFilterState.Invalid);
        viewModel.IsMatch("a.txt").Should().BeTrue();

        viewModel.DeleteCommand.Execute(null);

        viewModel.Text.Should().BeEmpty();
        viewModel.State.Should().Be(FileFilterState.None);
        _changes.Should().Be(2);
    }

    [Test]
    public void A_full_path_in_the_working_directory_should_be_cut_to_the_path_in_it()
    {
        FileFilterViewModel viewModel = NewViewModel();
        viewModel.WorkingDirectory = () => "/work/Repo";

        viewModel.Text = "/work/repo/src/a.cs";

        viewModel.Text.Should().Be("src/a.cs", "upstream compares the folder ignoring case");
    }

    [Test]
    public void A_filter_that_left_files_should_go_first_in_the_drop_down_up_to_upstreams_limit()
    {
        FileFilterViewModel viewModel = NewViewModel();
        viewModel.Filters.Should().HaveCount(2, "upstream's two samples");

        for (int i = 0; i < FileFilterViewModel.MaxFilters; i++)
        {
            viewModel.SetFilter($"f{i}");
            viewModel.Shown(fileCount: 1);
        }

        viewModel.SetFilter("nothing");
        viewModel.Shown(fileCount: 0);

        viewModel.Filters.Should().HaveCount(FileFilterViewModel.MaxFilters);
        viewModel.Filters[0].Should().Be("f9");
        viewModel.Filters.Should().NotContain("nothing");
        viewModel.Text.Should().Be("nothing");
    }

    [Test]
    public void File_names_only_should_match_the_name_or_the_name_before_a_rename()
    {
        _preferences.TruncatePathMethod = TruncatePathMethod.FileNameOnly;
        FileFilterViewModel viewModel = NewViewModel();

        viewModel.SetFilter("^a");

        viewModel.IsMatch("docs/a.md").Should().BeTrue();
        viewModel.IsMatch("all/b.md").Should().BeFalse("the folder is not shown");
        viewModel.IsMatch("docs/b.md", oldPath: "old/a.md").Should().BeTrue();
    }

    private FileFilterViewModel NewViewModel(TimeSpan? typingDelay = null)
    {
        FileFilterViewModel viewModel = new(_preferences) { TypingDelay = typingDelay ?? TimeSpan.Zero };
        viewModel.FilterChanged += (_, _) => _changes++;
        return viewModel;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("Timed out");
            }

            await Task.Delay(10);
        }
    }
}
