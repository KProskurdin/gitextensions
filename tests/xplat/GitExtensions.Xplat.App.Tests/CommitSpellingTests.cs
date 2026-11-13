using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Xplat.Core.Spelling;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

/// <summary>
///  Upstream <c>EditNetSpell</c>'s spelling checker in the commit message box.
/// </summary>
internal sealed class CommitSpellingTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    private TestRepository _repo = null!;

    [SetUp]
    public void Setup()
    {
        _repo = new TestRepository();
        Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Reset();
        _repo.Dispose();
    }

    [AvaloniaTest]
    public void Misspelled_words_get_their_wave_after_a_pause_and_the_menu_corrects_them()
    {
        CommitWindow commit = OpenCommitWindow();
        TextBox message = Find<TextBox>(commit, "CommitMessageBox");
        WaitUntil(() => commit.SpellingDictionary == "en-US");

        message.Text = "Fix speling in teh code";

        WaitUntil(() => Marked(commit, message).SequenceEqual(["speling", "teh"]));

        message.CaretIndex = 6;
        IReadOnlyList<object?> items = OpenMenu(message);
        Header(items[0]).Should().Be("spelling", "the suggestions come first, in bold");
        items.Select(Header).Should().ContainInOrder("Add to dictionary", "Ignore word", "Remove word",
            "_Word wrap (except subject line)", null, "Cut", "Copy", "Paste", "Delete", "Select all", null, "Dictionary", null,
            "Mark ill formed lines", "Provide auto completion");
        Click(items, "spelling");

        message.Text.Should().Be("Fix spelling in teh code");
        WaitUntil(() => Marked(commit, message).SequenceEqual(["teh"]));

        message.CaretIndex = 17;
        Click(OpenMenu(message), "Ignore word");

        WaitUntil(() => commit.MessageUnderlines!.Misspelled.Count == 0);
        commit.Close();
    }

    [AvaloniaTest]
    public void Remove_word_takes_the_misspelled_word_out_of_the_message()
    {
        CommitWindow commit = OpenCommitWindow();
        TextBox message = Find<TextBox>(commit, "CommitMessageBox");
        WaitUntil(() => commit.SpellingDictionary == "en-US");
        message.Text = "Fix the the qwzx code";
        WaitUntil(() => Marked(commit, message).SequenceEqual(["qwzx"]));

        message.CaretIndex = 13;
        Click(OpenMenu(message), "Remove word");

        message.Text.Should().Be("Fix the the  code");
        commit.Close();
    }

    [AvaloniaTest]
    public void Ill_formed_lines_are_marked_until_the_menu_turns_it_off()
    {
        CommitWindow commit = OpenCommitWindow();
        TextBox message = Find<TextBox>(commit, "CommitMessageBox");
        WaitUntil(() => commit.SpellingDictionary == "en-US");

        message.Text = new string('a', 55) + "\nsecond line";

        WaitUntil(() => commit.MessageUnderlines!.IllFormed.SequenceEqual([new TextRange(50, 5), new TextRange(56, 11)]));

        Click(OpenMenu(message), "Mark ill formed lines");

        WaitUntil(() => commit.MessageUnderlines!.IllFormed.Count == 0);
        TestAppBuilder.Preferences.MarkIllFormedLinesInCommitMsg.Should().BeFalse();
        commit.Close();
    }

    [AvaloniaTest]
    public void A_dictionary_chosen_in_the_menu_is_kept_for_the_repository()
    {
        CommitWindow commit = OpenCommitWindow();
        TextBox message = Find<TextBox>(commit, "CommitMessageBox");
        WaitUntil(() => commit.SpellingDictionary == "en-US");
        message.Text = "Das Haus ist rot";
        WaitUntil(() => commit.MessageUnderlines!.Misspelled.Count > 0);

        MenuItem dictionaries = OpenMenu(message).OfType<MenuItem>().Single(item => Header(item) == "Dictionary");
        dictionaries.Items.OfType<MenuItem>().Select(Header).Should().StartWith("None").And.Contain(["de-DE", "en-US"]);
        dictionaries.Items.OfType<MenuItem>().Single(item => Header(item) == "en-US").IsChecked.Should().BeTrue();
        dictionaries.Items.OfType<MenuItem>().Single(item => Header(item) == "de-DE")
            .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

        WaitUntil(() => commit.SpellingDictionary == "de-DE" && commit.MessageUnderlines!.Misspelled.Count == 0);
        WaitUntil(() => GitUI.ThreadHelper.JoinableTaskFactory.Run(() => SpellingDictionarySetting.LoadAsync(_repo.Path))
            == "de-DE");
        commit.Close();
    }

    private static void Reset() => TestAppBuilder.Preferences.MarkIllFormedLinesInCommitMsg = true;

    private static IEnumerable<string> Marked(CommitWindow commit, TextBox message)
        => commit.MessageUnderlines!.Misspelled.Select(range => message.Text!.Substring(range.Start, range.Length));

    private static IReadOnlyList<object?> OpenMenu(TextBox message)
    {
        MenuFlyout menu = (MenuFlyout)message.ContextFlyout!;
        menu.ShowAt(message);
        Dispatcher.UIThread.RunJobs();
        List<object?> items = [.. menu.Items];
        menu.Hide();
        return items;
    }

    private static string? Header(object? item)
        => item is MenuItem { Header: TextBlock text } ? text.Text : (item as MenuItem)?.Header as string;

    private static void Click(IReadOnlyList<object?> items, string header)
        => UiClicks.Raise(items.OfType<MenuItem>().First(item => Header(item) == header), MenuItem.ClickEvent);

    private CommitWindow OpenCommitWindow()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "changed");
        _repo.Run("add", "a.txt");
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        window.OpenRepository(_repo.Path);
        WaitUntil(() => !window.IsBusy);
        WaitUntil(() => Find<Button>(window, "CommitDialogButton").IsEnabled);
        UiClicks.Raise(Find<Button>(window, "CommitDialogButton"), Button.ClickEvent);
        WaitUntil(() => window.OwnedWindows.OfType<CommitWindow>().Any());
        return window.OwnedWindows.OfType<CommitWindow>().Single();
    }

    private static T Find<T>(Window window, string name) where T : Control
        => window.FindControl<T>(name) ?? throw new InvalidOperationException($"Control {name} not found");

    private static void WaitUntil(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow + _timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("Timed out waiting for the UI");
            }

            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(20);
        }
    }
}
