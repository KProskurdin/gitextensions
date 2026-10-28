using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using GitExtensions.Xplat.Core.CommitHistory;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class ArchiveWindowTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private TestRepository _repo = null!;
    private string _first = "";
    private string _second = "";
    private readonly List<string> _archived = [];
    private readonly List<string> _suggestions = [];

    [SetUp]
    public void SetUp()
    {
        _repo = new TestRepository();
        _first = _repo.Run("rev-parse", "HEAD~1").Trim();
        _second = _repo.Run("rev-parse", "HEAD").Trim();
        _archived.Clear();
        _suggestions.Clear();
    }

    [TearDown]
    public void TearDown() => _repo.Dispose();

    [AvaloniaTest]
    public void Save_as_archives_the_revision_with_the_paths_in_the_chosen_format()
    {
        (Window owner, ArchiveWindow window) = Open(diffRevision: null);
        Find<TextBlock>(window.FindControl<CommitSummaryView>("commitSummaryUserControl1")!, "MessageText").Text
            .Should().Be("second");
        Find<CheckBox>(window, "checkBoxPathFilter").IsChecked = true;
        Find<TextBox>(window, "textBoxPaths").Text = "a.txt";
        Find<RadioButton>(window, "_NO_TRANSLATE_radioButtonFormatTar").IsChecked = true;

        Click(window, "buttonArchiveRevision");

        WaitUntil(() => _archived.Count == 1);
        _suggestions.Should().Equal($"{Path.GetFileName(_repo.Path)}_{_second}_a_txt|tar");
        _archived[0].Should().Be($"archive --format=\"tar\" {_second} --output \"out.tar\" \"a.txt\"");
        WaitUntil(() => !window.IsVisible);
        owner.Close();
    }

    [AvaloniaTest]
    public void The_revision_filter_archives_the_files_changed_since_the_second_revision()
    {
        (Window owner, ArchiveWindow window) = Open(diffRevision: _first);
        Find<CheckBox>(window, "checkboxRevisionFilter").IsChecked.Should().BeTrue();
        Find<HeaderedContentControl>(window, "gbDiffRevision").Header.Should().Be(_first[..8]);
        Find<TextBlock>(window, "labelMessage").Text.Should().Be("first");

        Find<CheckBox>(window, "checkBoxPathFilter").IsChecked = true;
        Find<CheckBox>(window, "checkboxRevisionFilter").IsChecked.Should().BeFalse("the filters exclude each other");
        Find<CheckBox>(window, "checkboxRevisionFilter").IsChecked = true;
        Find<TextBox>(window, "textBoxPaths").IsEnabled.Should().BeFalse();
        Click(window, "buttonArchiveRevision");

        WaitUntil(() => _archived.Count == 1);
        _archived[0].Should().EndWith("--output \"out.zip\" \"a.txt\"");
        owner.Close();
    }

    [AvaloniaTest]
    public void The_revision_filter_needs_a_second_revision()
    {
        (Window owner, ArchiveWindow window) = Open(diffRevision: null);
        Find<CheckBox>(window, "checkboxRevisionFilter").IsChecked = true;

        Click(window, "buttonArchiveRevision");

        WaitUntil(() => window.OwnedWindows.OfType<MessageBoxWindow>().Any());
        Find<SelectableTextBlock>(window.OwnedWindows.OfType<MessageBoxWindow>().Single(), "MessageText").Text
            .Should().Be("You need to choose a target revision.");
        _archived.Should().BeEmpty();
        owner.Close();
    }

    [AvaloniaTest]
    public void The_commit_chooser_preselects_follows_parents_and_returns_the_choice()
    {
        Window owner = new();
        owner.Show();
        ChooseCommitWindow choose = new(_repo.Path, _first, TestAppBuilder.Preferences);
        Task<CommitRow?> result = choose.ShowDialog<CommitRow?>(owner);
        WaitUntil(() => choose.CommitsLoaded.IsCompleted);
        ListBox grid = Find<ListBox>(choose, "revisionGrid");
        ((CommitListItem)grid.SelectedItem!).Row.Subject.Should().Be("first");
        Find<StackPanel>(choose, "flowLayoutPanelParents").IsVisible.Should().BeFalse("the root has no parent");

        grid.SelectedItem = grid.Items.Cast<CommitListItem>().Single(item => item.Row.Subject == "second");
        HyperlinkButton parent = Find<HyperlinkButton>(choose, "linkLabelParent");
        parent.Content.Should().Be(_first[..8]);
        Find<HyperlinkButton>(choose, "linkLabelParent2").IsVisible.Should().BeFalse();
        parent.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        ((CommitListItem)grid.SelectedItem!).Row.Subject.Should().Be("first");
        grid.RaiseEvent(new TappedEventArgs(InputElement.DoubleTappedEvent, null!));

        WaitUntil(() => result.IsCompleted);
        result.Result!.Hash.Should().Be(_first);
        owner.Close();
    }

    private (Window Owner, ArchiveWindow Window) Open(string? diffRevision)
    {
        Window owner = new();
        owner.Show();
        ArchiveWindow window = new(_repo.Path, "HEAD", diffRevision, null, TestAppBuilder.Preferences, arguments =>
        {
            _archived.Add(arguments);
            return Task.CompletedTask;
        })
        {
            PickOutput = (suggestion, extension) =>
            {
                _suggestions.Add($"{suggestion}|{extension}");
                return Task.FromResult<string?>($"out.{extension}");
            },
        };
        _ = window.ShowDialog(owner);
        WaitUntil(() => window.RevisionsLoaded.IsCompleted);
        return (owner, window);
    }

    private static void Click(Window window, string name)
        => Find<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static T Find<T>(Control parent, string name)
        where T : Control
        => parent.FindControl<T>(name) ?? throw new InvalidOperationException($"No control named {name}");

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
        }
    }
}
