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
        bool gitPageShown = false;
        viewModel.GitPageRequested += (_, _) => gitPageShown = true;

        viewModel.SelectedHomeChoice = HomeChoice.Other;
        viewModel.OtherHome = "";
        await viewModel.OkCommand.ExecuteAsync(null);

        gitPageShown.Should().BeTrue();
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

    private SettingsViewModel New(FakeDialogHost host, bool isWindows = false)
    {
        SettingsViewModel viewModel = new(host, _preferences, new SettingsViewModelServices
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
        }, isWindows);

        // Showing the Git page sets the environment once; the tests look at what saving does.
        _steps.Clear();
        return viewModel;
    }
}
