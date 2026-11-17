using AwesomeAssertions;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class HotkeysPageViewModelTests
{
    private readonly List<string> _saved = [];

    [SetUp]
    public void SetUp() => _saved.Clear();

    [Test]
    public void Apply_should_write_the_changes_of_each_window_into_what_is_stored()
    {
        HotkeysPageViewModel page = New();
        HotkeyRow commit = page.Groups[0].Rows[0];

        commit.Capture("gesture", "Ctrl+K");

        commit.Text.Should().Be("Ctrl+K");
        (commit.IsChanged, commit.Gesture).Should().Be((true, "gesture"));
        page.Apply("stored").Should().Be("stored+Browse:Commit=Ctrl+K");
        _saved.Should().Equal(["Browse"], "only the window with a change is written");
    }

    [Test]
    public void Clear_should_remove_the_keys()
    {
        HotkeysPageViewModel page = New();
        HotkeyRow push = page.Groups[0].Rows[1];

        push.ClearCommand.Execute(null);

        (push.Text, push.Gesture, push.IsChanged).Should().Be(("", null, true));
        page.Apply(null).Should().Be("+Browse:Push=");
    }

    [Test]
    public void Reset_should_show_the_defaults_and_drop_what_was_stored()
    {
        HotkeysPageViewModel page = New();
        page.Groups[0].Rows[0].Capture("gesture", "Ctrl+K");

        page.ResetToDefaultsCommand.Execute(null);

        page.Groups.SelectMany(group => group.Rows).Select(row => row.Text).Should().Equal("Ctrl+Space", "Ctrl+Up", "F5");
        page.Groups.SelectMany(group => group.Rows).Should().NotContain(row => row.IsChanged);
        page.Apply("stored").Should().BeNull("nothing stored is kept, not even the hotkeys of what the shell does not have");
    }

    [Test]
    public void A_window_without_hotkeys_to_change_should_not_be_listed()
        => New().Groups.Select(group => group.Title).Should().Equal("Browse", "Commit");

    private HotkeysPageViewModel New()
        => new(
        [
            Group("Browse", new HotkeyRow("Commit", "Commit", "Ctrl+C", "Ctrl+Space"), new HotkeyRow("Push", "Push", "Ctrl+P", "Ctrl+Up")),
            Group("Commit", new HotkeyRow("Refresh", "Refresh", "F5", "F5")),
            Group("Empty"),
        ]);

    private HotkeyGroup Group(string title, params HotkeyRow[] rows)
        => new(title, rows, (stored, changed) =>
        {
            _saved.Add(title);
            return stored + string.Concat(changed.Select(row => $"+{title}:{row.Command}={row.Text}"));
        });
}
