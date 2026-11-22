using AwesomeAssertions;
using GitExtensions.Extensibility.Settings;
using GitExtensions.Xplat.Core.Settings;
using NSubstitute;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

// The page's own logic with an in-memory git config; the window's tests cover it with git.
internal sealed class GitConfigPageViewModelTests
{
    private readonly List<string> _errors = [];
    private MemoryConfig _config = null!;
    private bool _gitFound;

    [SetUp]
    public void SetUp()
    {
        _errors.Clear();
        _config = new MemoryConfig();
        _gitFound = true;
    }

    [Test]
    public async Task LoadAsync_should_show_the_levels_values_with_the_plain_tool_when_no_gui_tool_is_set()
    {
        _config.Values[(ConfigScope.Global, "user.name")] = "Ann";
        _config.Values[(ConfigScope.Global, "merge.tool")] = "kdiff3";
        _config.Values[(ConfigScope.Global, GitConfigPageViewModel.AutoCrlfKey)] = "INPUT";
        GitConfigPageViewModel page = New();

        await page.LoadAsync(SettingLevel.Global);

        (page.IsLoaded, page.IsLoading, page.UserName, page.MergeTool, page.HasMergeTool, page.HasDiffTool).Should()
            .Be((true, false, "Ann", "kdiff3", true, false));
        page.AutoCrlf.Should().Be(GitConfigPageViewModel.AutoCrlfInput);
        page.ShowsCredentialHelper.Should().BeTrue();
        page.ListsLoaded.IsCompleted.Should().BeTrue();
    }

    [Test]
    public async Task SaveAsync_should_write_only_what_changed_and_nothing_at_the_effective_level()
    {
        _config.Values[(ConfigScope.Global, "user.name")] = "Ann";
        _config.Values[(ConfigScope.Global, "user.email")] = "ann@example.com";
        GitConfigPageViewModel page = New();
        await page.LoadAsync(SettingLevel.Global);

        page.UserName = " Bob ";
        await page.ChangeLevelAsync(SettingLevel.Effective);
        page.UserEmail = "ignored@example.com";
        await page.SaveAsync();

        _config.Writes.Should().Equal(((ConfigScope.Global, "user.name"), "Bob"));
        page.ShowsCredentialHelper.Should().BeFalse("the effective settings give only the last helper");
    }

    [Test]
    public async Task Several_credential_helpers_should_be_shown_but_not_edited()
    {
        _config.All[(ConfigScope.Global, GitConfigPageViewModel.CredentialHelperKey)] = ["cache", "store"];
        GitConfigPageViewModel page = New();
        await page.LoadAsync(SettingLevel.Global);

        page.CredentialHelper.Should().Be("cache, store");
        page.CanEditCredentialHelper.Should().BeFalse();
        page.CredentialHelper = "changed";
        await page.SaveAsync();

        _config.Writes.Should().BeEmpty();
    }

    [Test]
    public async Task Without_git_the_page_should_read_nothing_and_say_so()
    {
        _gitFound = false;
        GitConfigPageViewModel page = New();

        await page.LoadAsync(SettingLevel.Global);

        (page.IsGitFound, page.IsLoaded, page.IsLoading).Should().Be((false, false, false));
        await page.SaveAsync();
        _config.Writes.Should().BeEmpty();
    }

    private GitConfigPageViewModel New()
    {
        IDiffMergeToolCatalog tools = Substitute.For<IDiffMergeToolCatalog>();
        Task<IReadOnlyList<string>> none = Task.FromResult<IReadOnlyList<string>>([]);
        tools.GetAvailableAsync(Arg.Any<bool>()).Returns(none);
        return new GitConfigPageViewModel(_config, repositoryPath: null, () => "git", _ => Task.FromResult(_gitFound), tools,
            _errors.Add);
    }

    private sealed class MemoryConfig : IGitConfigService
    {
        public Dictionary<(ConfigScope Scope, string Key), string> Values { get; } = [];

        public Dictionary<(ConfigScope Scope, string Key), IReadOnlyList<string>> All { get; } = [];

        public List<((ConfigScope Scope, string Name) Key, string Value)> Writes { get; } = [];

        public Task<string> GetAsync(ConfigScope scope, string key, string? repositoryPath = null)
            => Task.FromResult(Values.GetValueOrDefault((scope, key), ""));

        public Task<IReadOnlyList<string>> GetAllAsync(ConfigScope scope, string key, string? repositoryPath = null)
            => Task.FromResult(All.GetValueOrDefault((scope, key), []));

        public Task SetAsync(ConfigScope scope, string key, string value, string? repositoryPath = null)
        {
            Values[(scope, key)] = value;
            Writes.Add(((scope, key), value));
            return Task.CompletedTask;
        }
    }
}
