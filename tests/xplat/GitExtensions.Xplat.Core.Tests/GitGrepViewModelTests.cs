using AwesomeAssertions;
using GitExtensions.Xplat.Core.Diff;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class GitGrepViewModelTests
{
    private readonly List<string> _searches = [];
    private InMemoryAppPreferences _preferences = null!;
    private FakeWindow _window = null!;

    [SetUp]
    public void SetUp()
    {
        _searches.Clear();
        _preferences = new InMemoryAppPreferences();
        _window = new FakeWindow();
    }

    [Test]
    public async Task Typing_should_search_after_the_delay_and_remember_the_search_first()
    {
        GitGrepViewModel grep = New();

        grep.BoxText = "need";
        grep.BoxText = "needle";
        await grep.SearchCompleted;

        _searches.Should().ContainSingle().Which.Should().Contain("needle");
        (grep.Searched, grep.IsDeleteVisible).Should().Be(("needle", true));
        grep.Searches.Should().Equal("needle");

        grep.PickedSearch = "other";
        await grep.SearchCompleted;
        grep.Searches.Should().Equal("other", "needle");
    }

    [Test]
    public async Task Delete_should_clear_the_box_and_the_search_at_once()
    {
        GitGrepViewModel grep = New();
        grep.BoxText = "needle";
        await grep.SearchCompleted;

        grep.ClearCommand.Execute(null);
        await grep.SearchCompleted;

        (grep.BoxText, grep.Searched, grep.IsDeleteVisible).Should().Be(("", "", false));
        _searches.Should().HaveCount(2).And.EndWith("");
    }

    [Test]
    public void The_button_should_toggle_the_box_with_upstreams_input_box_choice_and_focus_it()
    {
        _preferences.FindInFilesGitGrepTypeIndex = GitGrepViewModel.UsingBox;
        GitGrepViewModel grep = New();
        int focused = 0;
        grep.BoxFocusRequested += (_, _) => focused++;

        grep.ToggleFromButtonCommand.Execute(null);

        (grep.IsBoxVisible, _preferences.ShowFindInCommitFilesGitGrep, focused, _window.IsOpen).Should()
            .Be((true, true, 1, false));

        grep.ToggleFromButtonCommand.Execute(null);

        grep.IsBoxVisible.Should().BeFalse();
    }

    [Test]
    public void Using_both_should_show_the_box_and_the_window_with_the_searches()
    {
        GitGrepViewModel grep = New();

        grep.SetFindUsingCommand.Execute(GitGrepViewModel.UsingBoth);

        (_preferences.FindInFilesGitGrepTypeIndex, grep.UsesBoth, grep.UsesBox).Should()
            .Be((GitGrepViewModel.UsingBoth, true, false));
        (grep.IsBoxVisible, _window.IsOpen, _window.ShowBox).Should().Be((true, true, true));
    }

    [Test]
    public async Task A_git_grep_setting_should_be_stored_shown_and_searched_again()
    {
        GitGrepViewModel grep = New();
        grep.BoxText = "needle";
        await grep.SearchCompleted;

        grep.ToggleMatchCaseCommand.Execute(null);
        grep.SetPatternOptionCommand.Execute("--fixed-strings");
        await grep.SearchCompleted;

        (_preferences.GitGrepIgnoreCase, grep.MatchCase).Should().Be((true, false));
        grep.OptionsHeader.Should().Be("Options: --fixed-strings");
        _searches.Should().HaveCount(3, "each setting searches again");
    }

    [Test]
    public async Task The_window_should_search_and_put_its_search_in_the_box()
    {
        GitGrepViewModel grep = New();

        grep.SearchFromWindow("needle");
        await grep.SearchCompleted;

        (grep.BoxText, grep.Searched).Should().Be(("needle", "needle"));
        _searches.Should().ContainSingle();
    }

    private GitGrepViewModel New()
        => new(_preferences, _searches.Add, _ => _window) { TypingDelay = TimeSpan.FromMilliseconds(1) };

    private sealed class FakeWindow : IGitGrepWindow
    {
        public bool IsOpen { get; private set; }

        public bool ShowBox { get; private set; }

        public void Show() => IsOpen = true;

        public void Close() => IsOpen = false;

        public void SetExpression(string text)
        {
        }

        public void SetSearchItems(IReadOnlyList<string> searches)
        {
        }

        public void SetShowBox(bool visible) => ShowBox = visible;
    }
}
