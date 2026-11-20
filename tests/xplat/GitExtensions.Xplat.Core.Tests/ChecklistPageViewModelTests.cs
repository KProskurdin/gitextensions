using AwesomeAssertions;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class ChecklistPageViewModelTests
{
    private static readonly ChecklistFacts _facts = new()
    {
        UserName = "Test",
        UserEmail = "test@example.com",
        MergeTool = "meld",
        MergeToolKnownToGit = true,
        Translation = "English",
    };

    private readonly List<string> _steps = [];
    private readonly List<string> _pages = [];
    private readonly List<Exception> _errors = [];
    private bool _checkAtStartup;

    [SetUp]
    public void SetUp()
    {
        _steps.Clear();
        _pages.Clear();
        _errors.Clear();
        _checkAtStartup = true;
    }

    [Test]
    public async Task RunAsync_should_show_a_row_per_check_in_upstreams_colors_with_Repair_on_the_wrong_ones()
    {
        ChecklistPageViewModel checklist = New(() => Task.FromResult(_facts));

        await checklist.RunAsync();

        checklist.Rows.Select(row => (row.Item, row.NeedsRepair)).Should().Equal(
            (ChecklistItem.Git, true), (ChecklistItem.UserName, false), (ChecklistItem.MergeTool, false),
            (ChecklistItem.DiffTool, true), (ChecklistItem.Language, false));
        checklist.Entries.Should().Equal(checklist.Rows.Select(row => row.Entry));
        checklist.IsValid.Should().BeFalse();
        ChecklistRow userName = checklist.Rows[1];
        userName.Background.Should().Be(System.Drawing.Color.FromArgb(128, 255, 128));
        userName.Foreground.Should().Be(System.Drawing.Color.Black);
    }

    [Test]
    public async Task Repair_should_show_the_page_to_fix_the_setting_on()
    {
        ChecklistPageViewModel checklist = New(() => Task.FromResult(_facts));
        await checklist.RunAsync();

        await checklist.RepairCommand.ExecuteAsync(checklist.Rows.Single(row => row.Item == ChecklistItem.DiffTool));
        await checklist.RepairCommand.ExecuteAsync(checklist.Rows.Single(row => row.Item == ChecklistItem.Git));

        _pages.Should().Equal("GitConfigSettingsPage", "GitSettingsPage");
        _steps.Should().Equal(["read"], "nothing is saved for a page to show");
    }

    [Test]
    public async Task Repair_of_the_language_should_choose_one_then_save_and_check_again()
    {
        ChecklistPageViewModel checklist = New(() => Task.FromResult(_facts));
        await checklist.RunAsync();

        await checklist.RepairCommand.ExecuteAsync(checklist.Rows.Single(row => row.Item == ChecklistItem.Language));

        _steps.Should().Equal("read", "language", "save", "read");
    }

    [Test]
    public async Task Save_and_rescan_should_store_the_values_before_the_checks_read_them()
    {
        ChecklistPageViewModel checklist = New(() => Task.FromResult(_facts));

        await checklist.SaveAndRescanCommand.ExecuteAsync(null);

        _steps.Should().Equal("save", "read");
        checklist.Rows.Should().NotBeEmpty();
    }

    [Test]
    public async Task A_run_started_later_should_win_over_an_earlier_one()
    {
        TaskCompletionSource<ChecklistFacts> first = new();
        Queue<Task<ChecklistFacts>> reads = new([first.Task, Task.FromResult(_facts with { Translation = "German" })]);
        ChecklistPageViewModel checklist = New(reads.Dequeue);

        Task earlier = checklist.RunAsync();
        await checklist.RunAsync();
        first.SetResult(_facts);
        await earlier;

        checklist.Entries.Single(entry => entry.Item == ChecklistItem.Language).Text.Should()
            .Be(string.Format(SettingsChecklist.LanguageConfigured, "German"));
    }

    [Test]
    public async Task The_startup_box_should_show_the_setting_and_store_a_change_at_once()
    {
        ChecklistPageViewModel checklist = New(() => Task.FromResult(_facts));

        checklist.CheckAtStartup.Should().BeTrue();
        await checklist.RunAsync();
        checklist.CheckAtStartup.Should().BeTrue("a setting is wrong");
        checklist.CheckAtStartup = false;

        _checkAtStartup.Should().BeFalse();
    }

    [Test]
    public async Task A_check_with_every_setting_right_should_turn_the_startup_check_off()
    {
        ChecklistPageViewModel checklist = New(() => Task.FromResult(_facts with
        {
            GitVersion = GitCommands.Git.GitVersion.LastRecommendedVersion,
            Editor = "vi",
            DiffTool = "meld",
            DiffToolKnownToGit = true,
        }));

        await checklist.RunAsync();

        checklist.IsValid.Should().BeTrue();
        checklist.CheckAtStartup.Should().BeFalse();
        _checkAtStartup.Should().BeFalse();
    }

    [Test]
    public async Task An_error_should_be_shown_on_the_window()
    {
        ChecklistPageViewModel checklist = New(() => throw new InvalidOperationException("no git"));

        await checklist.SaveAndRescanCommand.ExecuteAsync(null);

        _errors.Select(error => error.Message).Should().Equal("no git");
    }

    private ChecklistPageViewModel New(Func<Task<ChecklistFacts>> readFacts)
        => new(new ChecklistPageServices
        {
            ReadFacts = () =>
            {
                _steps.Add("read");
                return readFacts();
            },
            Save = () =>
            {
                _steps.Add("save");
                return Task.CompletedTask;
            },
            GoToPage = _pages.Add,
            ChooseLanguage = () =>
            {
                _steps.Add("language");
                return Task.CompletedTask;
            },
            OpenUri = _ => Task.CompletedTask,
            ShowError = _errors.Add,
            ReadCheckAtStartup = () => _checkAtStartup,
            WriteCheckAtStartup = value => _checkAtStartup = value,
        });
}
