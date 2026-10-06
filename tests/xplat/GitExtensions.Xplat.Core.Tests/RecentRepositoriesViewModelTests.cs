using AwesomeAssertions;
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
    public void Same_should_compare_case_as_the_file_system_does()
    {
        RecentRepositoryPaths.Same("/work/Repo", "/work/repo").Should().Be(OperatingSystem.IsWindows());
    }

    private sealed class FailingStore : IRecentRepositoryStore
    {
        public Task<IReadOnlyList<string>> LoadAsync() => Fail();

        public Task<IReadOnlyList<string>> AddAsync(string repositoryPath) => Fail();

        public Task<IReadOnlyList<string>> RemoveAsync(string repositoryPath) => Fail();

        private static Task<IReadOnlyList<string>> Fail()
            => Task.FromException<IReadOnlyList<string>>(new IOException("settings are read-only"));
    }
}
