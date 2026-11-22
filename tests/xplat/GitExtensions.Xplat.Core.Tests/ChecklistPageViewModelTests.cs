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
    private readonly List<string> _messages = [];
    private bool _checkAtStartup;

    [SetUp]
    public void SetUp()
    {
        _steps.Clear();
        _pages.Clear();
        _errors.Clear();
        _messages.Clear();
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
    public async Task Repair_of_the_shell_extension_should_register_it_and_check_again()
    {
        Platform.InMemoryWindowsInstallation installation = new() { FilesExist = true };
        ChecklistPageViewModel checklist = New(() => Task.FromResult(WindowsFacts(installation)), installation);
        await checklist.RunAsync();
        ChecklistRow shellExtension = checklist.Rows.Single(row => row.Item == ChecklistItem.ShellExtension);
        shellExtension.NeedsRepair.Should().BeTrue();

        await checklist.RepairCommand.ExecuteAsync(shellExtension);

        installation.Registrations.Should().Be(1);
        _steps.Should().Equal("read", "read");
        checklist.Rows.Single(row => row.Item == ChecklistItem.ShellExtension).NeedsRepair.Should().BeFalse();
    }

    [Test]
    public async Task Repair_of_the_install_folder_should_store_the_apps_and_check_again()
    {
        Platform.InMemoryWindowsInstallation installation = new() { InstallDir = @"C:\Other", AppDirectory = @"C:\App" };
        ChecklistPageViewModel checklist = New(() => Task.FromResult(WindowsFacts(installation)), installation);
        await checklist.RunAsync();

        await checklist.RepairCommand.ExecuteAsync(
            checklist.Rows.Single(row => row.Item == ChecklistItem.GitExtensionsInstall));

        installation.InstallDir.Should().Be(@"C:\App");
        checklist.Rows.Single(row => row.Item == ChecklistItem.GitExtensionsInstall).NeedsRepair.Should().BeFalse();
    }

    [Test]
    public async Task A_failed_registration_should_be_shown_and_a_declined_one_not()
    {
        Platform.InMemoryWindowsInstallation installation = new()
        {
            FilesExist = true, RegisterError = new InvalidOperationException("regsvr32 exited with code 5."),
        };
        ChecklistPageViewModel checklist = New(() => Task.FromResult(WindowsFacts(installation)), installation);
        await checklist.RunAsync();
        ChecklistRow shellExtension = checklist.Rows.Single(row => row.Item == ChecklistItem.ShellExtension);

        await checklist.RepairCommand.ExecuteAsync(shellExtension);
        installation.RegisterError = new OperationCanceledException();
        await checklist.RepairCommand.ExecuteAsync(shellExtension);

        _errors.Select(error => error.Message).Should().Equal("regsvr32 exited with code 5.");
        _steps.Should().Equal(["read", "read"], "a declined prompt checks again, a failure does not");
    }

    [Test]
    public async Task Repair_of_git_on_windows_should_say_which_command_runs_show_the_page_then_save_and_check_again()
    {
        ChecklistPageViewModel checklist = New(() => Task.FromResult(_facts), locate: item =>
            Task.FromResult<string?>(item == ChecklistItem.Git ? @"C:\Git\cmd\git.exe" : null));
        await checklist.RunAsync();

        await checklist.RepairCommand.ExecuteAsync(checklist.Rows.Single(row => row.Item == ChecklistItem.Git));

        _messages.Should().Equal(@"Locate git: Git can be run using: C:\Git\cmd\git.exe");
        _pages.Should().Equal("GitSettingsPage");
        _steps.Should().Equal("read", "save", "read");
    }

    [Test]
    public async Task Repair_on_windows_should_say_what_was_not_found_and_show_the_Git_page()
    {
        ChecklistPageViewModel checklist = New(() => Task.FromResult(_facts with { IsWindows = true }),
            locate: _ => Task.FromResult<string?>(null));
        await checklist.RunAsync();

        await checklist.RepairCommand.ExecuteAsync(checklist.Rows.Single(row => row.Item == ChecklistItem.Git));
        await checklist.RepairCommand.ExecuteAsync(checklist.Rows.Single(row => row.Item == ChecklistItem.LinuxTools));
        await checklist.RepairCommand.ExecuteAsync(checklist.Rows.Single(row => row.Item == ChecklistItem.Ssh));

        _messages.Should().Equal(
            $"Locate git: {Platform.WindowsGit.SolveGitCommandFailed1}{Environment.NewLine}{Platform.WindowsGit.SolveGitCommandFailed2}",
            $"Locate linux tools: {Platform.WindowsGit.LinuxToolsNotFound1}{Environment.NewLine}{Platform.WindowsGit.LinuxToolsNotFound2}");
        _pages.Should().Equal("GitSettingsPage", "GitSettingsPage", "SshSettingsPage");
        _steps.Should().Equal(["read"], "nothing was found, so nothing is saved");
    }

    [Test]
    public async Task Repair_of_the_linux_tools_should_say_where_sh_is_then_save_and_check_again()
    {
        ChecklistPageViewModel checklist = New(() => Task.FromResult(_facts with { IsWindows = true }),
            locate: _ => Task.FromResult<string?>(@"C:\Git\usr\bin\"));
        await checklist.RunAsync();

        await checklist.RepairCommand.ExecuteAsync(checklist.Rows.Single(row => row.Item == ChecklistItem.LinuxTools));

        _messages.Should().Equal(@"Locate linux tools: Command sh can be run using: C:\Git\usr\bin\sh");
        _pages.Should().BeEmpty("upstream stays on the Checklist");
        _steps.Should().Equal("read", "save", "read");
    }

    [Test]
    public async Task Repair_of_putty_should_say_all_was_found_and_stay()
    {
        ChecklistPageViewModel checklist = New(() => Task.FromResult(_facts with { IsWindows = true }),
            locate: _ => Task.FromResult<string?>(@"C:\PuTTY\plink.exe"));
        await checklist.RunAsync();

        await checklist.RepairCommand.ExecuteAsync(checklist.Rows.Single(row => row.Item == ChecklistItem.Ssh));

        _messages.Should().Equal($"PuTTY: {Platform.WindowsGit.PuttyFoundAuto}");
        _pages.Should().BeEmpty();
        _steps.Should().Equal(["read"], "upstream does not check again here");
    }

    [Test]
    public async Task An_error_should_be_shown_on_the_window()
    {
        ChecklistPageViewModel checklist = New(() => throw new InvalidOperationException("no git"));

        await checklist.SaveAndRescanCommand.ExecuteAsync(null);

        _errors.Select(error => error.Message).Should().Equal("no git");
    }

    // The facts of the installation as they stand, as the reader reads them each time.
    private static ChecklistFacts WindowsFacts(Platform.IWindowsInstallation installation)
        => _facts with
        {
            IsWindows = true,
            Installation = new InstallationFacts(installation.InstallDir, installation.AppDirectory, InstallDirExists: true,
                DebuggerAttached: false, installation.IsShellExtensionRegistered(), installation.ShellExtensionFilesExist()),
        };

    private ChecklistPageViewModel New(Func<Task<ChecklistFacts>> readFacts,
        Platform.IWindowsInstallation? installation = null, Func<ChecklistItem, Task<string?>>? locate = null)
        => new(new ChecklistPageServices
        {
            Installation = installation ?? new Platform.InMemoryWindowsInstallation(),
            Locate = locate,
            ShowMessage = (text, caption) =>
            {
                _messages.Add($"{caption}: {text}");
                return Task.CompletedTask;
            },
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
