using AwesomeAssertions;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class PullAutoStashTests
{
    private static readonly PullRequest _request = new("origin", "main", PullAction.Merge, Prune: false, AutoStash: true);

    private readonly List<string> _steps = [];
    private InMemoryAppPreferences _preferences = null!;
    private bool _hasChanges;
    private bool _stashSucceeds;
    private bool _inTheMiddle;
    private StashPopAnswer _answer = null!;

    [SetUp]
    public void SetUp()
    {
        _steps.Clear();
        _preferences = new InMemoryAppPreferences();
        _hasChanges = true;
        _stashSucceeds = true;
        _inTheMiddle = false;
        _answer = new StashPopAnswer(Pop: true, Remember: false);
    }

    [Test]
    public async Task PullAsync_should_stash_pull_without_gits_autostash_then_ask_and_pop()
    {
        PullRequest? pulled = null;

        bool done = await New().PullAsync(_request, request =>
        {
            pulled = request;
            _steps.Add("pull");
            return Task.FromResult(true);
        });

        done.Should().BeTrue();
        pulled.Should().Be(_request with { AutoStash = false });
        _steps.Should().Equal("stash untracked=False", "pull", "ask", "pop");
        _preferences.AutoPopStashAfterPull.Should().BeNull("the answer was not to be remembered");
    }

    [Test]
    public async Task PullAsync_should_remember_an_answer_with_dont_show_again_and_keep_the_stash_on_No()
    {
        _answer = new StashPopAnswer(Pop: false, Remember: true);
        _preferences.IncludeUntrackedFilesInAutoStash = true;

        await New().PullAsync(_request, Pull(true));

        _steps.Should().Equal("stash untracked=True", "pull", "ask");
        _preferences.AutoPopStashAfterPull.Should().BeFalse();
    }

    [TestCase(true, "pop")]
    [TestCase(false, null)]
    public async Task PullAsync_should_follow_a_remembered_answer_without_asking(bool pop, string? last)
    {
        _preferences.AutoPopStashAfterPull = pop;

        await New().PullAsync(_request, Pull(true));

        _steps.Should().NotContain("ask");
        _steps.LastOrDefault(step => step == "pop").Should().Be(last);
    }

    [Test]
    public async Task PullAsync_should_keep_the_stash_after_a_failed_pull_or_one_left_in_the_middle()
    {
        await New().PullAsync(_request, Pull(false));
        _inTheMiddle = true;
        await New().PullAsync(_request, Pull(true));

        _steps.Should().Equal("stash untracked=False", "pull", "stash untracked=False", "pull");
    }

    [Test]
    public async Task PullAsync_should_not_pull_when_the_stash_failed()
    {
        _stashSucceeds = false;

        bool done = await New().PullAsync(_request, Pull(true));

        done.Should().BeFalse();
        _steps.Should().Equal("stash untracked=False");
    }

    [Test]
    public async Task PullAsync_should_pull_alone_without_changes_without_auto_stash_and_for_a_fetch()
    {
        _hasChanges = false;
        await New().PullAsync(_request, Pull(true));
        _hasChanges = true;
        await New().PullAsync(_request with { AutoStash = false }, Pull(true));
        await New().PullAsync(_request with { Action = PullAction.FetchOnly }, Pull(true));

        _steps.Should().Equal("pull", "pull", "pull");
    }

    private Func<PullRequest, Task<bool>> Pull(bool succeeds) => _ =>
    {
        _steps.Add("pull");
        return Task.FromResult(succeeds);
    };

    private PullAutoStash New() => new(_preferences, new PullAutoStashServices
    {
        HasLocalChanges = () => Task.FromResult(_hasChanges),
        Stash = includeUntracked =>
        {
            _steps.Add($"stash untracked={includeUntracked}");
            return Task.FromResult(_stashSucceeds);
        },
        IsInTheMiddleOfAction = () => Task.FromResult(_inTheMiddle),
        AskPop = () =>
        {
            _steps.Add("ask");
            return Task.FromResult(_answer);
        },
        Pop = () =>
        {
            _steps.Add("pop");
            return Task.FromResult(true);
        },
    });
}
