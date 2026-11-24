using AwesomeAssertions;
using GitExtensions.Extensibility.Settings;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

// Upstream's Git > Advanced page with an in-memory git config; the window's test covers it with the settings window.
internal sealed class GitConfigAdvancedPageViewModelTests
{
    private MemoryConfig _config = null!;

    [SetUp]
    public void SetUp() => _config = new MemoryConfig();

    [Test]
    public void The_page_should_list_upstreams_settings_with_their_values_and_help()
    {
        GitConfigAdvancedPageViewModel page = new(_config, repositoryPath: null, supportsUpdateRefs: true);

        page.Settings.Select(setting => setting.Key).Should().Equal("pull.ff", "pull.rebase", "fetch.prune",
            "merge.autostash", "rebase.autostash", "rebase.autosquash", "rebase.updaterefs", "rerere.enabled",
            "rerere.autoupdate", "status.showuntrackedfiles", "core.untrackedcache", "core.fsmonitor");
        page.Setting("pull.ff").Values.Should().Equal("", "false", "true", "only");
        page.Setting("status.showuntrackedfiles").Values.Should().Equal("", "no", "normal", "all");
        page.Setting("core.untrackedcache").HelpUrl.Should()
            .Be("https://git-scm.com/docs/git-config#Documentation/git-config.txt-coreuntrackedcache");
    }

    [Test]
    public async Task LoadAsync_should_show_the_levels_values_with_the_keys_in_the_texts()
    {
        _config.Values[(ConfigScope.Global, "pull.rebase")] = "yes";
        _config.Values[(ConfigScope.Global, "rerere.enabled")] = "sometimes";
        GitConfigAdvancedPageViewModel page = new(_config, repositoryPath: null, supportsUpdateRefs: false,
            text => "[t]" + text);

        await page.LoadAsync(SettingLevel.Global);

        (page.Setting("pull.rebase").Value, page.Setting("rerere.enabled").Value, page.Setting("fetch.prune").Value)
            .Should().Be(("yes", "sometimes", ""));
        page.Setting("pull.rebase").Text.Should().Be("[t]Rebase local branch when pulling (instead of merge) [pull.rebase]");
        page.Setting("rebase.updaterefs").IsVisible.Should().BeFalse("upstream hides it for a git without update-refs");
    }

    [Test]
    public async Task SaveAsync_should_write_only_the_changed_values_and_remove_an_emptied_key()
    {
        _config.Values[(ConfigScope.Global, "pull.rebase")] = "yes";
        _config.Values[(ConfigScope.Global, "rerere.enabled")] = "true";
        GitConfigAdvancedPageViewModel page = new(_config, null, supportsUpdateRefs: true);
        await page.LoadAsync(SettingLevel.Global);

        page.Setting("pull.ff").Value = "only";
        page.Setting("rerere.enabled").Value = "";
        await page.SaveAsync();

        _config.Writes.Should().Equal(((ConfigScope.Global, "pull.ff"), "only"), ((ConfigScope.Global, "rerere.enabled"), ""));

        await page.SaveAsync();

        _config.Writes.Should().HaveCount(2, "a second save compares with what the first wrote");
    }

    [Test]
    public async Task Each_level_should_keep_its_edits_and_the_effective_level_should_write_nothing()
    {
        _config.Values[(ConfigScope.Local, "merge.autostash")] = "true";
        GitConfigAdvancedPageViewModel page = new(_config, "repo", supportsUpdateRefs: true);
        await page.LoadAsync(SettingLevel.Local);

        page.Setting("merge.autostash").Value = "false";
        await page.ChangeLevelAsync(SettingLevel.Effective);
        page.Setting("merge.autostash").Value.Should().BeEmpty("the effective value is read as not set in the fake");
        page.Setting("pull.rebase").Value = "true";
        await page.ChangeLevelAsync(SettingLevel.Local);

        page.Setting("merge.autostash").Value.Should().Be("false", "the level keeps what was edited there");
        await page.SaveAsync();

        _config.Writes.Should().Equal(((ConfigScope.Local, "merge.autostash"), "false"));
    }

    private sealed class MemoryConfig : IGitConfigService
    {
        public Dictionary<(ConfigScope Scope, string Name), string> Values { get; } = [];

        public List<((ConfigScope Scope, string Name) Key, string Value)> Writes { get; } = [];

        public Task<string> GetAsync(ConfigScope scope, string key, string? repositoryPath = null)
            => Task.FromResult(Values.GetValueOrDefault((scope, key), ""));

        public Task<IReadOnlyList<string>> GetAllAsync(ConfigScope scope, string key, string? repositoryPath = null)
            => Task.FromResult<IReadOnlyList<string>>([]);

        public Task SetAsync(ConfigScope scope, string key, string value, string? repositoryPath = null)
        {
            Values[(scope, key)] = value;
            Writes.Add(((scope, key), value));
            return Task.CompletedTask;
        }
    }
}
