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

    [TestCase("true", true)]
    [TestCase("Yes", true)]
    [TestCase("on", true)]
    [TestCase("1", true)]
    [TestCase("false", false)]
    [TestCase("NO", false)]
    [TestCase("0", false)]
    [TestCase("", false)]
    [TestCase("merges", null)]
    public void StateOf_should_read_gits_value_as_upstreams_check_box(string value, bool? state)
        => GitConfigAdvancedPageViewModel.StateOf(value).Should().Be(state);

    [Test]
    public async Task LoadAsync_should_show_the_levels_states_with_the_keys_in_the_texts()
    {
        _config.Values[(ConfigScope.Global, "pull.rebase")] = "yes";
        _config.Values[(ConfigScope.Global, "rebase.updaterefs")] = "true";
        _config.Values[(ConfigScope.Global, "rerere.enabled")] = "sometimes";
        GitConfigAdvancedPageViewModel page = new(_config, repositoryPath: null, supportsUpdateRefs: false,
            text => "[t]" + text);

        await page.LoadAsync(SettingLevel.Global);

        page.Settings.Select(setting => setting.IsChecked).Should()
            .Equal(true, false, false, false, false, true, null, false);
        page.PullRebase.Text.Should().Be("[t]Rebase local branch when pulling (instead of merge) [pull.rebase]");
        page.RebaseUpdateRefs.IsVisible.Should().BeFalse("upstream hides it for a git without update-refs");
    }

    [Test]
    public async Task SaveAsync_should_write_only_the_changed_states_and_remove_a_key_left_neither()
    {
        _config.Values[(ConfigScope.Global, "pull.rebase")] = "yes";
        _config.Values[(ConfigScope.Global, "rerere.enabled")] = "true";
        GitConfigAdvancedPageViewModel page = new(_config, null, supportsUpdateRefs: true);
        await page.LoadAsync(SettingLevel.Global);

        page.FetchPrune.IsChecked = true;
        page.RerereEnabled.IsChecked = null;
        await page.SaveAsync();

        _config.Writes.Should().Equal(((ConfigScope.Global, "fetch.prune"), "true"), ((ConfigScope.Global, "rerere.enabled"), ""));

        await page.SaveAsync();

        _config.Writes.Should().HaveCount(2, "a second save compares with what the first wrote");
    }

    [Test]
    public async Task Each_level_should_keep_its_edits_and_the_effective_level_should_write_nothing()
    {
        _config.Values[(ConfigScope.Local, "merge.autostash")] = "true";
        GitConfigAdvancedPageViewModel page = new(_config, "repo", supportsUpdateRefs: true);
        await page.LoadAsync(SettingLevel.Local);

        page.MergeAutoStash.IsChecked = false;
        await page.ChangeLevelAsync(SettingLevel.Effective);
        page.MergeAutoStash.IsChecked.Should().BeFalse("the effective value is read as not set in the fake");
        page.PullRebase.IsChecked = true;
        await page.ChangeLevelAsync(SettingLevel.Local);

        page.MergeAutoStash.IsChecked.Should().BeFalse("the level keeps what was edited there");
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
