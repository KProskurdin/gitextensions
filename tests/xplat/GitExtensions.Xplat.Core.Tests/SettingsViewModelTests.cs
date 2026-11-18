using AwesomeAssertions;
using GitCommands.Settings;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Settings;
using GitExtensions.Xplat.Core.BuildServer;
using GitExtensions.Xplat.Core.Scripts;
using GitExtensions.Xplat.Core.Settings;
using GitExtUtils.GitUI.Theming;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

// The pages whose editors the window drives (fonts, hotkeys, scripts, plugins, git config) are covered by the window's
// tests; these cover the one-value-per-preference pages and OK, Apply and Cancel.
internal sealed class SettingsViewModelTests
{
    private readonly List<string> _steps = [];
    private InMemoryAppPreferences _preferences = null!;

    [SetUp]
    public void SetUp()
    {
        _steps.Clear();
        _preferences = new InMemoryAppPreferences();
    }

    [Test]
    public async Task OK_should_store_the_values_around_the_windows_pages_and_close_with_true()
    {
        _preferences.MulticolorBranches = false;
        FakeDialogHost host = new();
        SettingsViewModel viewModel = New(host);
        viewModel.MulticolorBranches.Should().BeFalse();

        viewModel.MulticolorBranches = true;
        viewModel.SelectedPullAction = PullActionOption.All.Single(option => option.Action == GitPullAction.Rebase);
        await viewModel.OkCommand.ExecuteAsync(null);

        _preferences.MulticolorBranches.Should().BeTrue();
        _preferences.DefaultPullAction.Should().Be(GitPullAction.Rebase);
        _steps.Should().Equal("before", "environment", "after close=True");
        _preferences.SaveCount.Should().Be(1);
        host.Result.Should().Be(true);
    }

    [Test]
    public async Task Cancel_after_Apply_should_report_the_save()
    {
        FakeDialogHost host = new();
        SettingsViewModel viewModel = New(host);

        await viewModel.ApplyCommand.ExecuteAsync(null);
        host.IsClosed.Should().BeFalse();
        viewModel.CancelCommand.Execute(null);

        _steps.Should().EndWith("after close=False");
        host.Result.Should().Be(true);
    }

    [Test]
    public async Task An_unchecked_commit_limit_should_be_stored_as_no_limit()
    {
        _preferences.MaxRevisionGraphCommits = 5000;
        SettingsViewModel viewModel = New(new FakeDialogHost());
        (viewModel.LimitCommits, viewModel.MaxRevisionGraphCommits).Should().Be((true, 5000m));

        viewModel.LimitCommits = false;
        await viewModel.ApplyCommand.ExecuteAsync(null);

        _preferences.MaxRevisionGraphCommits.Should().Be(0);
    }

    [Test]
    public void The_submodule_status_should_be_offered_only_with_a_git_status()
    {
        _preferences.ShowGitStatusInBrowseToolbar = true;
        _preferences.ShowGitStatusForArtificialCommits = false;
        _preferences.ShowSubmoduleStatus = true;
        SettingsViewModel viewModel = New(new FakeDialogHost());
        viewModel.ShowSubmoduleStatus.Should().BeTrue();

        viewModel.ShowGitStatusInBrowseToolbar = false;

        (viewModel.CanShowSubmoduleStatus, viewModel.ShowSubmoduleStatus).Should().Be((false, false));
    }

    [Test]
    public async Task No_dictionary_should_be_stored_as_upstreams_none_and_the_colorblind_variation_as_a_variation()
    {
        SettingsViewModel viewModel = New(new FakeDialogHost());
        viewModel.Dictionaries.Should().Equal(Spelling.CommitSpellChecker.NoDictionary, "en-US");

        viewModel.SelectedDictionary = viewModel.Dictionaries[0];
        viewModel.Colorblind = true;
        await viewModel.ApplyCommand.ExecuteAsync(null);

        _preferences.Dictionary.Should().Be(SettingsViewModel.NoDictionaryStored);
        _preferences.ThemeVariations.Should().Equal(ThemeVariations.Colorblind);
    }

    [Test]
    public async Task Another_HOME_without_a_folder_should_show_the_Git_page_and_store_nothing()
    {
        SettingsViewModel viewModel = New(new FakeDialogHost(), isWindows: true);
        viewModel.PageTree.GoToPage("GeneralSettingsPage");

        viewModel.SelectedHomeChoice = HomeChoice.Other;
        viewModel.OtherHome = "";
        await viewModel.OkCommand.ExecuteAsync(null);

        viewModel.PageTree.ShownNode!.Page.Should().Be(SettingsViewModel.GitPage);
        viewModel.ErrorText.Should().NotBeEmpty();
        _steps.Should().BeEmpty();
        _preferences.SaveCount.Should().Be(0);
    }

    [Test]
    public void ShowLanguages_should_keep_a_stored_translation_whose_file_is_gone()
    {
        SettingsViewModel viewModel = New(new FakeDialogHost());

        viewModel.ShowLanguages("Klingon");

        viewModel.Languages.Should().Equal(SettingsViewModel.English, "German", "Klingon");
        viewModel.SelectedLanguage.Should().Be("Klingon");
    }

    [Test]
    public async Task Only_a_changed_font_and_the_questions_should_be_stored()
    {
        _preferences.SetFont(AppFont.Code, new FontSetting("Missing Font", 11, Bold: true));
        SettingsViewModel viewModel = New(new FakeDialogHost());
        FontChoice code = viewModel.Fonts.Single(font => font.Font == AppFont.Code);
        code.Families.Should().Equal(FontChoice.DefaultChoice, "Missing Font", "Arial", "Courier New");
        FontChoice commit = viewModel.Fonts.Single(font => font.Font == AppFont.Commit);
        (commit.IsChosen, code.IsChosen).Should().Be((false, true));

        commit.Family = "Arial";
        commit.Italic = true;
        viewModel.ConfirmationChoices.First(choice => choice.Confirmation == Confirmation.StashDrop).Asks = false;
        await viewModel.ApplyCommand.ExecuteAsync(null);

        _preferences.GetFont(AppFont.Commit).Should().Be(new FontSetting("Arial", 9, Italic: true));
        _preferences.GetFont(AppFont.Code).Should().Be(new FontSetting("Missing Font", 11, Bold: true));
        _preferences.Asks(Confirmation.StashDrop).Should().BeFalse();
    }

    [Test]
    public void The_scripts_page_should_add_move_and_delete_and_store_only_a_change()
    {
        SettingsViewModel viewModel = New(new FakeDialogHost());
        ScriptsPageViewModel scripts = viewModel.ScriptsPage;
        scripts.Selected!.Name.Should().Be("First");
        scripts.MoveUpCommand.CanExecute(null).Should().BeFalse();
        List<IReadOnlyList<ScriptDefinition>> saved = [];
        scripts.StoreIfChanged(saved.Add).Should().BeFalse();

        scripts.AddCommand.Execute(null);
        scripts.MoveUpCommand.Execute(null);
        scripts.Selected.Name.Should().Be(ScriptListEditor.NewScriptName);
        scripts.Items.Select(item => item.Name).Should().Equal("First", ScriptListEditor.NewScriptName, "Second");
        scripts.DeleteCommand.Execute(null);

        scripts.Items.Select(item => item.Name).Should().Equal("First", "Second");
        scripts.Selected.Name.Should().Be("Second");
        scripts.StoreIfChanged(saved.Add).Should().BeFalse("the list is as loaded again");
        scripts.Selected.Name = "Renamed";
        scripts.StoreIfChanged(saved.Add).Should().BeTrue();
        saved.Should().ContainSingle().Which.Select(script => script.Name).Should().Equal("First", "Renamed");
    }

    [Test]
    public void The_revision_links_page_should_add_select_and_remove_definitions_and_links()
    {
        RevisionLinksPageViewModel links = New(new FakeDialogHost()).RevisionLinksPage;
        (links.Items.Count, links.HasSelection, links.RemoveCommand.CanExecute(null)).Should().Be((0, false, false));

        links.AddCommand.Execute(null);
        links.AddFormatCommand.Execute(null);
        links.AddCommand.Execute(null);
        links.Items.Should().ContainSingle("a second new definition is the one listed");
        links.Selected!.Name = "First";
        links.AddCommand.Execute(null);

        links.Items.Should().HaveCount(2);
        links.Selected.Should().BeSameAs(links.Items[1]);
        links.Items[0].Formats.Should().ContainSingle();
        links.Selected = links.Items[0];
        links.RemoveFormat(links.Items[0].Formats[0]);
        links.Items[0].Formats.Should().BeEmpty();
        links.RemoveCommand.Execute(null);
        links.Items.Should().ContainSingle();
        links.Selected.Should().BeSameAs(links.Items[0]);
    }

    [Test]
    public async Task The_build_server_page_should_store_only_at_a_level_and_report_a_chosen_type()
    {
        Dictionary<SettingLevel, DistributedSettings> levels = [];
        BuildServerPageViewModel page = new(
            level => levels.TryGetValue(level, out DistributedSettings? settings) ? settings : levels[level] =
                new FileRevisionLinkStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".settings")).Open(null),
            () => Task.FromResult<IReadOnlyList<BuildServerType>>([new BuildServerType("Jenkins")]),
            hasRepository: true);
        int chosen = 0;
        page.TypeChosen += (_, _) => chosen++;

        await page.LoadAsync();

        page.Types.Select(type => type.DisplayName).Should().Equal(BuildServerPageViewModel.NoBuildServer, "Jenkins");
        (page.SelectedType!.Name, page.IsLoaded, chosen).Should().Be((null, true, 0));
        page.SelectedType = page.Types[1];
        page.IntegrationEnabled = true;
        chosen.Should().Be(1);
        page.Save().Should().BeFalse("the effective settings are shown read-only");

        page.ShowLevel(SettingLevel.Local);
        page.SelectedType = page.Types[1];
        page.IntegrationEnabled = true;
        page.Save().Should().BeTrue();

        BuildServerSettings.ServerName[levels[SettingLevel.Local]].Should().Be("Jenkins");
        BuildServerSettings.IntegrationEnabled[levels[SettingLevel.Local]].Should().BeTrue();
    }

    [Test]
    public async Task BrowsePath_should_put_the_chosen_path_in_its_box_and_keep_it_when_cancelled()
    {
        List<string> titles = [];
        string? chosen = "/usr/bin/git";
        SettingsViewModel viewModel = New(new FakeDialogHost(), services: services => services with
        {
            PickFile = (title, _) =>
            {
                titles.Add(title);
                return Task.FromResult<string?>(chosen);
            },
            PickFolder = title =>
            {
                titles.Add(title);
                return Task.FromResult<string?>(chosen);
            },
        });

        await viewModel.BrowsePathCommand.ExecuteAsync(SettingsPathField.GitCommand);
        chosen = "/opt/home";
        await viewModel.BrowsePathCommand.ExecuteAsync(SettingsPathField.OtherHome);
        chosen = null;
        await viewModel.BrowsePathCommand.ExecuteAsync(SettingsPathField.GitCommand);
        viewModel.GitConfigPage.DiffTool = "meld";
        chosen = "/usr/bin/meld";
        await viewModel.BrowsePathCommand.ExecuteAsync(SettingsPathField.DiffToolPath);

        viewModel.GitCommand.Should().Be("/usr/bin/git", "a cancelled picker leaves the box");
        viewModel.OtherHome.Should().Be("/opt/home");
        viewModel.GitConfigPage.DiffToolPath.Should().Be("/usr/bin/meld");
        titles.Should().Equal(SettingsViewModel.GitCommandTitle, SettingsViewModel.HomeTitle,
            SettingsViewModel.GitCommandTitle, "meld");
    }

    [Test]
    public async Task A_failing_picker_should_show_its_error_in_the_window()
    {
        SettingsViewModel viewModel = New(new FakeDialogHost(), services: services => services with
        {
            PickFile = (_, _) => throw new IOException("no picker"),
        });

        await viewModel.BrowsePathCommand.ExecuteAsync(SettingsPathField.Plink);

        viewModel.ErrorText.Should().Be("no picker");
    }

    [Test]
    public void The_help_and_folder_commands_should_open_the_manual_and_the_theme_folders()
    {
        List<string> opened = [];
        string user = Path.Combine(Path.GetTempPath(), "xplat-themes-" + Guid.NewGuid().ToString("N"));
        try
        {
            SettingsViewModel viewModel = New(new FakeDialogHost(), services: services => services with
            {
                OpenUrl = opened.Add,
                ManualUrl = (subfolder, anchor) => $"https://manual/{subfolder}#{anchor}",
                OpenFolder = opened.Add,
                AppThemesFolder = "/app/Themes",
                UserThemesFolder = user,
            });

            viewModel.OpenManualCommand.Execute("sorting-sort-author-date");
            viewModel.OpenThemesFolderCommand.Execute(false);
            viewModel.OpenThemesFolderCommand.Execute(true);

            opened[0].Should().Be("https://manual/settings#sorting-sort-author-date");
            opened.Skip(1).Should().Equal("/app/Themes", user);
            Directory.Exists(user).Should().BeTrue("upstream makes the user's folder before it shows it");
            viewModel.CanOpenUserThemesFolder.Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(user))
            {
                Directory.Delete(user);
            }
        }

        New(new FakeDialogHost()).CanOpenUserThemesFolder.Should().BeFalse("a portable app has no user folder");
    }

    [Test]
    public async Task PickScriptIconFile_should_set_the_selected_scripts_icon_file()
    {
        bool? images = null;
        SettingsViewModel viewModel = New(new FakeDialogHost(), services: services => services with
        {
            PickFile = (_, imagesFirst) =>
            {
                images = imagesFirst;
                return Task.FromResult<string?>("/icons/run.png");
            },
        });
        viewModel.ScriptsPage.Selected = viewModel.ScriptsPage.Items[1];

        await viewModel.PickScriptIconFileCommand.ExecuteAsync(null);

        viewModel.ScriptsPage.Items[1].IconFilePath.Should().Be("/icons/run.png");
        images.Should().BeTrue("upstream's picker offers images first");
    }

    private SettingsViewModel New(FakeDialogHost host, bool isWindows = false,
        Func<SettingsViewModelServices, SettingsViewModelServices>? services = null)
    {
        SettingsViewModelServices defaults = new()
        {
            Themes = [ThemeId.DefaultLight],
            Dictionaries = ["en-US"],
            FontFamilies = ["Arial", "Courier New"],
            Scripts = [new ScriptDefinition { Name = "First" }, new ScriptDefinition { Name = "Second" }],
            RevisionLinkEditorAt = _ => new RevisionLinkEditor(
                new FileRevisionLinkStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".settings")).Open(null)),
            LoadRemotes = () => Task.FromResult<IReadOnlyList<Remote>>([]),
            SettingsAt = _ => new FileRevisionLinkStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".settings")).Open(null),
            Translations = () => ["German"],
            SavePagesBeforeStore = () => _steps.Add("before"),
            SavePagesAfterStore = close =>
            {
                _steps.Add($"after close={close}");
                return Task.CompletedTask;
            },
            SetEnvironment = () => _steps.Add("environment"),
            ApplySshPath = _ => { },
        };
        SettingsViewModel viewModel = new(host, _preferences, services is null ? defaults : services(defaults), isWindows);

        // Showing the Git page sets the environment once; the tests look at what saving does.
        _steps.Clear();
        return viewModel;
    }
}
