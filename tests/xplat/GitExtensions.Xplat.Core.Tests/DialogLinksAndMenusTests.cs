using AwesomeAssertions;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Platform;
using GitExtensions.Xplat.Core.Presentation;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using NSubstitute;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

// The dialogs' links, the About window's actions, the recent repositories' menu and the commit picker.
internal sealed class DialogLinksAndMenusTests
{
    private static readonly ObjectId _tagged = ObjectId.Parse("1234567812345678123456781234567812345678");
    private static readonly ObjectId _head = ObjectId.Parse("abcdefabcdefabcdefabcdefabcdefabcdefabcd");

    [Test]
    public async Task The_about_windows_links_contributors_and_copy_should_go_to_the_host_and_the_window()
    {
        FakeDialogHost host = new();
        List<string> steps = [];
        AboutViewModel about = new(host, "Git Extensions", ["Ann"], new Random(1))
        {
            ShowContributors = () =>
            {
                steps.Add("contributors");
                return Task.CompletedTask;
            },
            CopyInformation = () =>
            {
                steps.Add("copy");
                return Task.CompletedTask;
            },
        };

        about.OpenUrlCommand.Execute(Contributors.ProjectUrl);
        await about.ShowContributorsWindowCommand.ExecuteAsync(null);
        await about.CopyEnvironmentInformationCommand.ExecuteAsync(null);

        host.OpenedUrls.Should().Equal(Contributors.ProjectUrl);
        steps.Should().Equal("contributors", "copy");
    }

    [Test]
    public void The_recent_repositories_menu_should_not_open_without_a_selection_and_name_its_items_by_list()
    {
        RecentRepositoriesSettingsViewModel settings = new(new FakeDialogHost(), Substitute.For<IRecentRepositoryStore>(),
            new InMemoryAppPreferences());

        IReadOnlyList<MenuEntry> entries = settings.MenuEntries("TopLB", () => []);

        settings.RefreshMenu(entries, []).Should().BeFalse();
        entries.Select(entry => entry.Name).Should().Equal("TopLBAnchorToTopMenuItem", "TopLBAnchorToRecentMenuItem",
            "TopLBRemoveAnchorMenuItem", "TopLBRemoveRecentMenuItem");
        entries[0].Text.Should().Be(FakeDialogHost.TranslatedMark + RecentRepositoriesSettingsViewModel.AnchorToTopText);
    }

    [Test]
    public async Task The_commit_picker_should_resolve_a_typed_ref_and_refuse_an_unknown_one()
    {
        IGitModule module = Substitute.For<IGitModule>();
        module.RevParse("v1").Returns(_tagged);
        module.GetCurrentCheckout().Returns(_head);
        module.GetCommitCountString(_head, _tagged.ToString()).Returns("+2");
        List<string> errors = [];
        CommitPickerViewModel picker = new()
        {
            Module = module,
            ShowError = (message, _) =>
            {
                errors.Add(message);
                return Task.CompletedTask;
            },
        };

        await picker.SetCommitAsync("v1");

        (picker.Selected, picker.HashText).Should().Be((_tagged, _tagged.ToShortString()));
        await WaitUntilAsync(() => picker.CountText == "+2");

        picker.HashText = "no-such-ref";
        await picker.SetCommitAsync("no-such-ref");

        errors.Should().Equal(CommitPickerViewModel.InvalidCommitHash);
        (picker.Selected, picker.HashText).Should().Be((_tagged, _tagged.ToShortString()), "the commit before is kept");
    }

    [Test]
    public async Task The_commit_pickers_button_should_choose_in_the_window_from_the_chosen_commit()
    {
        IGitModule module = Substitute.For<IGitModule>();
        module.RevParse(_head.ToString()).Returns(_head);
        List<string?> asked = [];
        CommitPickerViewModel picker = new()
        {
            Module = module,
            Selected = _tagged,
            PickCommit = selected =>
            {
                asked.Add(selected);
                return Task.FromResult<string?>(_head.ToString());
            },
        };

        await picker.PickCommand.ExecuteAsync(null);

        asked.Should().Equal(_tagged.ToString());
        picker.Selected.Should().Be(_head);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("Timed out");
            }

            await Task.Delay(10);
        }
    }
}
