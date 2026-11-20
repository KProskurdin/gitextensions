using AwesomeAssertions;
using GitCommands.Git;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class StartupSettingsCheckTests
{
    private const string AppEditor = "'/app/GitExtensions' fileeditor";

    private static readonly ChecklistFacts _good = new()
    {
        GitVersion = GitVersion.LastRecommendedVersion,
        UserName = "Test",
        UserEmail = "test@example.com",
        Editor = "vi",
        MergeTool = "meld",
        MergeToolKnownToGit = true,
        DiffTool = "meld",
        DiffToolKnownToGit = true,
        Translation = "English",
    };

    private readonly InMemoryAppPreferences _preferences = new();
    private readonly Dictionary<string, string?> _environment = [];
    private int _reads;

    [SetUp]
    public void SetUp()
    {
        _environment.Clear();
        _reads = 0;
        _preferences.CheckSettings = true;
    }

    [Test]
    public async Task RunAsync_should_turn_itself_off_without_opening_the_settings_when_all_is_right()
    {
        bool open = await New(_ => _good).RunAsync();

        open.Should().BeFalse();
        _preferences.CheckSettings.Should().BeFalse();
        _reads.Should().Be(1);
        _environment.Should().BeEmpty();
    }

    [Test]
    public async Task RunAsync_should_open_the_settings_when_a_setting_is_still_wrong()
    {
        bool open = await New(_ => _good with { UserEmail = null }).RunAsync();

        open.Should().BeTrue();
        _preferences.CheckSettings.Should().BeTrue("it stays on until every setting is right");
        _reads.Should().Be(2, "upstream checks again after AutoSolveAllSettings");
    }

    [Test]
    public async Task RunAsync_should_solve_a_missing_editor_with_the_app_and_check_again()
    {
        bool open = await New(environment => _good with { Editor = environment }).RunAsync();

        open.Should().BeFalse();
        _environment.Should().Equal(new Dictionary<string, string?> { [StartupSettingsCheck.EditorVariable] = AppEditor });
        _preferences.CheckSettings.Should().BeFalse();
    }

    [Test]
    public async Task RunAsync_with_the_check_off_should_only_solve_the_editor()
    {
        _preferences.CheckSettings = false;

        bool open = await New(_ => _good with { UserEmail = null, Editor = null }).RunAsync();

        open.Should().BeFalse();
        _reads.Should().Be(0);
        _environment.Should().ContainKey(StartupSettingsCheck.EditorVariable);
    }

    [Test]
    public async Task RunAsync_should_keep_a_configured_editor()
    {
        _preferences.CheckSettings = false;

        await New(_ => _good, editor: "code --wait").RunAsync();

        _environment.Should().BeEmpty();
    }

    // readFacts gets the editor variable this process has, so a solved editor shows in the next check.
    private StartupSettingsCheck New(Func<string?, ChecklistFacts> readFacts, string? editor = null)
        => new(_preferences,
            () =>
            {
                _reads++;
                return Task.FromResult(readFacts(_environment.GetValueOrDefault(StartupSettingsCheck.EditorVariable)));
            },
            () => Task.FromResult(editor ?? _environment.GetValueOrDefault(StartupSettingsCheck.EditorVariable)),
            AppEditor,
            (name, value) => _environment[name] = value);
}
