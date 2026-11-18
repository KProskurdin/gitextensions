using AwesomeAssertions;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Diff;
using GitExtensions.Xplat.Core.Settings;
using NSubstitute;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

// Reading the grid and showing a commit's tabs go through git and the window's viewers, so the window's tests cover them;
// these cover the options, the menu and the tabs it opens on.
internal sealed class FileHistoryViewModelTests
{
    private readonly List<BlameOptions> _blameChanges = [];
    private InMemoryAppPreferences _preferences = null!;

    [SetUp]
    public void SetUp()
    {
        _blameChanges.Clear();
        _preferences = new InMemoryAppPreferences
        {
            FileHistory = new FileHistoryOptions(LoadHistoryOnShow: false, LoadBlameOnShow: false),
        };
    }

    [Test]
    public void The_view_model_should_open_on_the_tab_asked_for_with_the_files_title()
    {
        FileHistoryViewModel viewModel = New(showBlame: true);

        viewModel.SelectedTabIndex.Should().Be((int)FileHistoryTab.Blame);
        viewModel.Title.Should().StartWith(FileHistoryDialog.TitlePrefix + "a.txt");
        viewModel.IsBlameTabVisible.Should().BeTrue();
        viewModel.CanSaveFile.Should().BeTrue();
        viewModel.CommitTabText.Should().Be(FakeDialogHost.TranslatedMark + FileHistoryDialog.CommitTabText);
    }

    [Test]
    public async Task LoadAsync_should_wait_for_Load_when_the_settings_say_so()
    {
        FileHistoryViewModel viewModel = New();

        await viewModel.LoadAsync();

        viewModel.IsHistoryVisible.Should().BeFalse();
        viewModel.Loaded.IsCompleted.Should().BeTrue();
    }

    [Test]
    public void ToggleOption_should_store_the_option_and_show_it()
    {
        FileHistoryViewModel viewModel = New();
        List<string?> changed = [];
        viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        viewModel.ToggleOptionCommand.Execute(FileHistoryOption.LoadHistoryOnShow);

        _preferences.FileHistory.LoadHistoryOnShow.Should().BeTrue();
        _preferences.SaveCount.Should().Be(1);
        viewModel.LoadHistoryOnShow.Should().BeTrue();
        changed.Should().Contain(nameof(FileHistoryViewModel.LoadHistoryOnShow));
        viewModel.IsHistoryVisible.Should().BeFalse("only a change of what is read reloads the history");
    }

    [Test]
    public async Task ToggleBlameOption_should_hand_the_toggled_options_to_the_Blame_tab()
    {
        _preferences.BlameOptions = new BlameOptions(ShowAuthor: false);
        FileHistoryViewModel viewModel = New();

        await viewModel.ToggleBlameOptionCommand.ExecuteAsync(BlameOption.ShowAuthorDate);

        // Upstream's rule: the line always shows the author or the date.
        _blameChanges.Should().Equal(new BlameOptions(ShowAuthor: true, ShowAuthorDate: false));
    }

    [Test]
    public void UpdateMenu_should_disable_the_commit_items_without_a_selection()
    {
        FileHistoryViewModel viewModel = New();

        viewModel.UpdateMenu();

        viewModel.Menu.Should().Be(new FileHistoryMenu(DiffToolRemoteLocal: false, OpenWithDifftool: false,
            ManipulateCommit: false, SaveAs: false, CopyToClipboard: false));
    }

    [Test]
    public async Task The_menu_and_toolstrip_commands_should_ask_the_window()
    {
        List<object> asked = [];
        FakeDialogHost host = new();
        FileHistoryViewModel viewModel = New(host: host, services: services => services with
        {
            RunGridCommand = command =>
            {
                asked.Add(command);
                return true;
            },
            ShowCommandLog = () => asked.Add("command log"),
            OpenReportedBuild = () => asked.Add("build report"),
        });

        viewModel.RunGridCommand.Execute(GridCommand.GoToParent);
        viewModel.ShowCommandLogCommand.Execute(null);
        viewModel.OpenReportedBuildCommand.Execute(null);
        await viewModel.QuickSearchHelpCommand.ExecuteAsync(null);

        asked.Should().Equal(GridCommand.GoToParent, "command log", "build report");
        host.Messages.Should().Equal(FakeDialogHost.TranslatedMark + FileHistoryViewModel.QuickSearchHelpText);
    }

    [Test]
    public async Task The_difftool_items_should_list_the_tools_under_both_items()
    {
        IDiffMergeToolCatalog catalog = Substitute.For<IDiffMergeToolCatalog>();
        IReadOnlyList<string> tools = ["meld", "vscode"];
        catalog.GetAvailableAsync(true, Arg.Any<CancellationToken>()).Returns(tools);
        _preferences.ShowAvailableDiffTools = true;
        FileHistoryViewModel viewModel = New(services: services => services with { DiffMergeTools = catalog });

        await viewModel.DiffTools.LoadAsync();

        viewModel.OpenWithDifftoolTools.Entries.Select(entry => entry.SearchText).Should().Equal("meld", "vscode", null, null);
        viewModel.DiffToolRemoteLocalTools.Entries.Should().HaveCount(4);
    }

    private FileHistoryViewModel New(bool showBlame = false, FakeDialogHost? host = null,
        Func<FileHistoryViewModelServices, FileHistoryViewModelServices>? services = null)
    {
        FileHistoryViewModelServices defaults = new()
        {
            CherryPick = (_, _) => Task.CompletedTask,
            Revert = (_, _) => Task.CompletedTask,
            ShowContent = _ => Task.CompletedTask,
            ChangeBlameOptions = options =>
            {
                _blameChanges.Add(options);
                return Task.CompletedTask;
            },
            PickSaveFile = (_, _) => Task.FromResult<string?>(null),
            History = new FakeCommitHistory(),
        };
        return new FileHistoryViewModel(host ?? new FakeDialogHost(), Substitute.For<IGitModule>(),
            Path.Combine(Path.GetTempPath(), "no-such-repository-" + Guid.NewGuid().ToString("N")), _preferences, "a.txt",
            revision: null, showBlame, services is null ? defaults : services(defaults));
    }
}
