using AwesomeAssertions;
using GitCommands;
using GitCommands.UserRepositoryHistory;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class RecentRepositoriesViewModelTests
{
    [Test]
    public async Task AddAsync_should_put_the_repository_first_and_keep_it_once()
    {
        RecentRepositoriesViewModel viewModel = new(new InMemoryRecentRepositoryStore());

        await viewModel.AddAsync("/work/one");
        await viewModel.AddAsync("/work/two");
        await viewModel.AddAsync("/work/one/");

        viewModel.Items.Select(item => item.Path).Should().Equal("/work/one/", "/work/two");
        viewModel.Items[0].Name.Should().Be("one");
    }

    [Test]
    public async Task The_list_should_show_each_repositorys_branch_once_it_is_read()
    {
        RecentRepositoriesViewModel viewModel = new(new InMemoryRecentRepositoryStore(),
            path => path == "/work/one" ? "main" : null);

        await viewModel.AddAsync("/work/two");
        await viewModel.AddAsync("/work/one");
        await viewModel.BranchesRead;

        viewModel.Items.Select(item => item.Branch).Should().Equal("main", null);
        viewModel.Items[0].Display.Should().Be("one [main]  (/work/one)");
        viewModel.Items[1].Display.Should().Be("two  (/work/two)");
    }

    [Test]
    public async Task RemoveAsync_should_drop_the_entry_with_or_without_a_trailing_separator()
    {
        RecentRepositoriesViewModel viewModel = new(new InMemoryRecentRepositoryStore());
        await viewModel.AddAsync("/work/one/");
        await viewModel.AddAsync("/work/two/");

        await viewModel.RemoveAsync("/work/one");

        viewModel.Items.Select(item => item.Path).Should().Equal("/work/two/");
    }

    [Test]
    public async Task A_failing_store_should_keep_the_list_and_report_the_error()
    {
        RecentRepositoriesViewModel viewModel = new(new FailingStore());

        await viewModel.AddAsync("/work/one");

        viewModel.Items.Should().BeEmpty();
        viewModel.ErrorMessage.Should().Be("settings are read-only");
    }

    [TestCase("/home/me/src/gitextensions/", "gitextensions")]
    [TestCase("/", "/")]
    [TestCase("repo", "repo")]
    public void DisplayName_should_be_the_folder_name(string path, string expected)
    {
        RecentRepositoryPaths.DisplayName(path).Should().Be(expected);
    }

    [Test]
    public void Caption_should_be_the_full_path_without_shortening()
    {
        string path = RootedOutsideHome("work", "repo");

        RecentRepositoryPaths.Caption(path, [], Splitter(ShorteningRecentRepoPathStrategy.None))
            .Should().Be(path + Path.DirectorySeparatorChar);
    }

    [Test]
    public void Caption_should_be_the_folder_name_with_the_most_significant_directory_strategy()
    {
        string path = RootedOutsideHome("work", "repo");

        RecentRepositoryPaths.Caption(path, [RootedOutsideHome("other", "tool")],
            Splitter(ShorteningRecentRepoPathStrategy.MostSignDir)).Should().Be("repo");
    }

    [Test]
    public void Caption_should_show_the_home_folder_as_a_tilde()
    {
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "src", "repo");

        RecentRepositoryPaths.Caption(path, [], Splitter(ShorteningRecentRepoPathStrategy.None))
            .Should().Be($"~{Path.DirectorySeparatorChar}src{Path.DirectorySeparatorChar}repo");
    }

    [Test]
    public void Same_should_compare_case_as_the_file_system_does()
    {
        RecentRepositoryPaths.Same("/work/Repo", "/work/repo").Should().Be(OperatingSystem.IsWindows());
    }

    // A path on a drive or root of its own, so it is never under the user's home folder.
    private static string RootedOutsideHome(params string[] parts)
        => Path.Combine([OperatingSystem.IsWindows() ? @"Q:\" : "/xplat-outside-home", .. parts]);

    // Every setting the caption depends on, so the user's settings file never changes a test.
    private static RecentRepoSplitter Splitter(ShorteningRecentRepoPathStrategy strategy) => new()
    {
        ShorteningStrategy = strategy,
        MaxTopRepositories = 0,
        HideTopRepositoriesFromRecentList = false,
        SortTopRepos = false,
        SortRecentRepos = false,
        RecentReposComboMinWidth = 0,
    };

    private sealed class FailingStore : IRecentRepositoryStore
    {
        public Task<IReadOnlyList<string>> LoadAsync() => Fail();

        public Task<IReadOnlyList<string>> AddAsync(string repositoryPath) => Fail();

        public Task<IReadOnlyList<string>> RemoveAsync(string repositoryPath) => Fail();

        public Task<RepositoryHistory> LoadHistoryAsync() => Task.FromException<RepositoryHistory>(new IOException("read-only"));

        public Task AssignCategoryAsync(string repositoryPath, string? category) => Fail();

        public Task RemoveFavouriteAsync(string repositoryPath) => Fail();

        public Task RemoveInvalidAsync(Func<string, bool> isValid) => Fail();

        public Task SaveRecentAsync(IReadOnlyList<RepositoryEntry> recent) => Fail();

        private static Task<IReadOnlyList<string>> Fail()
            => Task.FromException<IReadOnlyList<string>>(new IOException("settings are read-only"));
    }
}
