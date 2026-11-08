using System.Text;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class DiffMergeToolSettingsTests
{
    [Test]
    public void The_keys_should_be_upstreams()
    {
        DiffMergeToolSettings.PathKey(diff: false, "meld").Should().Be("mergetool.meld.path");
        DiffMergeToolSettings.CommandKey(diff: false, "meld").Should().Be("mergetool.meld.cmd");
        DiffMergeToolSettings.PathKey(diff: true, "meld").Should().Be("difftool.meld.path");
        DiffMergeToolSettings.CommandKey(diff: true, "meld").Should().Be("difftool.meld.cmd");
    }

    [Test]
    public void SuggestCommand_should_be_upstreams_for_the_path_on_the_page()
    {
        DiffMergeToolSettings.SuggestCommand("meld", diff: true, "/opt/bin/meld")
            .Should().Be("\"/opt/bin/meld\" \"$LOCAL\" \"$REMOTE\"");
        DiffMergeToolSettings.SuggestCommand("meld", diff: false, "/opt/bin/meld")
            .Should().Be("\"/opt/bin/meld\" \"$LOCAL\" \"$BASE\" \"$REMOTE\" --output \"$MERGED\"");
        DiffMergeToolSettings.SuggestCommand("", diff: true, "/opt/bin/meld").Should().BeEmpty();
        DiffMergeToolSettings.SuggestCommand("nosuchtool", diff: true, "/opt/bin/x").Should().BeEmpty("upstream knows no command for it");
    }

    [Test]
    public void IsRegistered_should_know_upstreams_tools_by_kind()
    {
        DiffMergeToolSettings.IsRegistered("meld", diff: true).Should().BeTrue();
        DiffMergeToolSettings.IsRegistered("nosuchtool", diff: true).Should().BeFalse();
    }

    [Test]
    public async Task ReadAsync_should_prefer_what_is_stored_at_the_level()
    {
        RecordingConfig config = new();
        config.Values[(ConfigScope.Global, "mergetool.meld.path")] = "/stored/meld";
        config.Values[(ConfigScope.Global, "mergetool.meld.cmd")] = "stored command";

        DiffMergeToolRow row = await DiffMergeToolSettings.ReadAsync(config, ConfigScope.Global, "meld", diff: false, null);

        row.Should().Be(new DiffMergeToolRow("/stored/meld", "stored command"));
    }

    [Test]
    public async Task ReadAsync_should_fall_back_to_what_upstream_finds_and_nothing_without_a_tool()
    {
        RecordingConfig config = new();

        DiffMergeToolRow row = await DiffMergeToolSettings.ReadAsync(config, ConfigScope.Global, "meld", diff: true, null);
        row.Path.Should().NotBeEmpty("upstream's last resort is the file name");
        row.Command.Should().EndWith("\"$LOCAL\" \"$REMOTE\"");

        (await DiffMergeToolSettings.ReadAsync(config, ConfigScope.Global, " ", diff: true, null))
            .Should().Be(new DiffMergeToolRow("", ""));
    }

    [Test]
    public void Selectable_should_leave_out_the_included_and_UTF7_and_repeat_nothing()
    {
        Dictionary<string, Encoding> included = new() { [Encoding.UTF8.WebName] = Encoding.UTF8 };

        IReadOnlyList<Encoding> selectable = AvailableEncodingsDialog.Selectable(included);

        selectable.Should().NotContain(encoding => encoding.WebName == Encoding.UTF8.WebName);
        selectable.Should().NotContain(encoding => encoding.WebName == "utf-7");
        selectable.Select(encoding => encoding.WebName).Should().OnlyHaveUniqueItems();
    }

    [Test]
    public void CanRemove_should_keep_the_default_encodings()
    {
        AvailableEncodingsDialog.CanRemove(null).Should().BeFalse();
        AvailableEncodingsDialog.CanRemove(new UTF8Encoding(false)).Should().BeFalse();
        AvailableEncodingsDialog.CanRemove(Encoding.ASCII).Should().BeFalse();
        AvailableEncodingsDialog.CanRemove(Encoding.Unicode).Should().BeFalse();
        AvailableEncodingsDialog.CanRemove(Encoding.Latin1).Should().BeTrue();
    }

    [Test]
    public void Apply_should_replace_the_list_by_web_name()
    {
        Dictionary<string, Encoding> available = new() { ["old"] = Encoding.ASCII };

        AvailableEncodingsDialog.Apply(available, [Encoding.Latin1]);

        available.Should().ContainSingle().Which.Key.Should().Be(Encoding.Latin1.WebName);
    }

    [Test]
    public async Task CanFindGitAsync_should_answer_for_a_git_and_not_for_anything_else()
    {
        (await GitConfigChoices.CanFindGitAsync(null)).Should().BeTrue("git is on the PATH of the tests");
        (await GitConfigChoices.CanFindGitAsync("/no/such/git")).Should().BeFalse();
    }

    private sealed class RecordingConfig : IGitConfigService
    {
        public Dictionary<(ConfigScope, string), string> Values { get; } = [];

        public Task<string> GetAsync(ConfigScope scope, string key, string? repositoryPath = null)
            => Task.FromResult(Values.GetValueOrDefault((scope, key), ""));

        public Task<IReadOnlyList<string>> GetAllAsync(ConfigScope scope, string key, string? repositoryPath = null)
            => Task.FromResult<IReadOnlyList<string>>([]);

        public Task SetAsync(ConfigScope scope, string key, string value, string? repositoryPath = null)
        {
            Values[(scope, key)] = value;
            return Task.CompletedTask;
        }
    }
}
