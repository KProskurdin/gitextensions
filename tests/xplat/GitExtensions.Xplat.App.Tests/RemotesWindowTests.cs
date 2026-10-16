using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class RemotesWindowTests
{
    private const string OriginUrl = "https://example.com/origin.git";

    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    private TestRepository _repo = null!;

    [SetUp]
    public void Setup()
    {
        _repo = new TestRepository();
        _repo.Run("remote", "add", "origin", OriginUrl);

        // Upstream keeps an inactive remote in a "-remote" section, which git does not read.
        File.AppendAllText(ConfigPath, "[-remote \"old\"]\n\turl = /srv/old.git\n");
        TestAppBuilder.RemoteUrls.Entries.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        TestAppBuilder.RemoteUrls.Entries.Clear();
        _repo.Dispose();
    }

    [AvaloniaTest]
    public void The_window_lists_the_remotes_in_upstreams_groups_and_shows_the_preselected_one()
    {
        RemotesWindow window = Open(preselectRemote: "origin");

        Rows(window).Select(row => (row.Text, row.IsHeader, row.IsInactive)).Should().Equal(
            ("Active", true, false), ("origin", false, false), ("Inactive", true, false), ("old", false, true));
        Find<HeaderedContentControl>(window, "DetailsGroup").Header.Should().Be("Edit Remote Details");
        Find<TextBox>(window, "NameBox").Text.Should().Be("origin");
        Find<ComboBox>(window, "UrlBox").Text.Should().Be(OriginUrl);
        Find<Button>(window, "DeleteButton").IsEnabled.Should().BeTrue();

        // As upstream, an inactive remote is shown but cannot be edited.
        Find<ListBox>(window, "RemotesList").SelectedItem = Rows(window).Single(row => row.Text == "old");

        Find<TextBox>(window, "NameBox").Text.Should().Be("old");
        Find<Grid>(window, "DetailsGrid").IsEnabled.Should().BeFalse();
        window.Close();
    }

    [AvaloniaTest]
    public void A_new_remote_is_saved_to_the_config_kept_in_the_url_history_and_fetched()
    {
        List<string> fetched = [];
        RemotesWindow window = Open(fetchRemote: remote =>
        {
            fetched.Add(remote);
            return Task.CompletedTask;
        });

        Click(window, "NewButton");
        Find<HeaderedContentControl>(window, "DetailsGroup").Header.Should().Be("Create New Remote");
        Find<TextBox>(window, "NameBox").Text = "fork";
        Find<ComboBox>(window, "UrlBox").Text = "https://example.com/fork.git";
        Click(window, "SaveButton");

        WaitUntil(() => fetched.Count == 1);
        fetched.Should().Equal("fork");
        _repo.Run("config", "--get", "remote.fork.url").Trim().Should().Be("https://example.com/fork.git");
        TestAppBuilder.RemoteUrls.Entries.Select(entry => entry.Path).Should().Contain("https://example.com/fork.git");
        WaitUntil(() => Rows(window).Any(row => row.Text == "fork"));
        Find<HeaderedContentControl>(window, "DetailsGroup").Header.Should().Be("Edit Remote Details");
        window.Close();
    }

    [AvaloniaTest]
    public void A_changed_name_renames_the_remote_on_save()
    {
        RemotesWindow window = Open(preselectRemote: "origin", fetchRemote: _ => Task.CompletedTask);

        Find<TextBox>(window, "NameBox").Text = "renamed";
        Click(window, "SaveButton");

        WaitUntil(() => Rows(window).Any(row => row.Text == "renamed"));
        _repo.Run("remote").Trim().Should().Be("renamed");
        _repo.Run("config", "--get", "remote.renamed.url").Trim().Should().Be(OriginUrl);
        window.Close();
    }

    [AvaloniaTest]
    public void Deactivate_moves_the_remote_to_the_inactive_group()
    {
        RemotesWindow window = Open(preselectRemote: "origin");

        Click(window, "ToggleStateButton");

        WaitUntil(() => Rows(window).Count(row => row.IsInactive) == 2);
        Rows(window).Where(row => row.IsInactive).Select(row => row.Text).Should().BeEquivalentTo("old", "origin");
        Rows(window).Should().NotContain(row => row.Text == "Active");
        File.ReadAllText(ConfigPath).Should().Contain("[-remote \"origin\"]");
        _repo.Run("remote").Trim().Should().BeEmpty();
        window.Close();
    }

    private string ConfigPath => Path.Combine(_repo.Path, ".git", "config");

    private RemotesWindow Open(string? preselectRemote = null, Func<string, Task>? fetchRemote = null)
    {
        RemotesWindow window = new(_repo.Path, TestAppBuilder.Preferences, fetchRemote, preselectRemote);
        window.Show();
        WaitUntil(() => window.RemotesLoaded.IsCompleted);
        return window;
    }

    private static IReadOnlyList<RemoteListRow> Rows(RemotesWindow window)
        => Find<ListBox>(window, "RemotesList").ItemsSource as IReadOnlyList<RemoteListRow> ?? [];

    private static void Click(Window window, string name)
        => Find<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

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
