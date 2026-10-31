using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class CreateTagWindowTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private TestRepository _repo = null!;

    [SetUp]
    public void SetUp() => _repo = new TestRepository();

    [TearDown]
    public void TearDown() => _repo.Dispose();

    [AvaloniaTest]
    public void An_annotated_tag_gets_its_message_and_the_message_file_is_removed()
    {
        List<(string Remote, string Tag)> pushed = [];
        (Window owner, CreateTagWindow window) = Open("HEAD~1", pushed);
        Find<TextBox>(window, "textBoxGpgKey").IsEnabled.Should().BeFalse();
        Find<TextBox>(window, "tagMessage").IsEnabled.Should().BeFalse("a lightweight tag has no message");

        Find<TextBox>(window, "textBoxTagName").Text = "v1.0";
        Find<ComboBox>(window, "annotate").SelectedIndex = 1;
        Find<TextBox>(window, "tagMessage").IsEnabled.Should().BeTrue();
        Find<TextBox>(window, "tagMessage").Text = "first release";
        Find<CheckBox>(window, "pushTag").IsChecked = true;
        Click(window, "Ok");

        WaitUntil(() => !window.IsVisible);
        _repo.Run("rev-parse", "v1.0^{commit}").Trim().Should().Be(_repo.Run("rev-parse", "HEAD~1").Trim());
        _repo.Run("tag", "-l", "--format=%(contents:subject)", "v1.0").Trim().Should().Be("first release");
        File.Exists(Path.Combine(_repo.Path, "TAGMESSAGE")).Should().BeFalse();
        WaitUntil(() => pushed.Count == 1);
        pushed.Should().Equal(("origin", "v1.0"));
        owner.Close();
    }

    [AvaloniaTest]
    public void The_push_box_names_the_remote_and_a_specific_key_enables_its_box()
    {
        (Window owner, CreateTagWindow window) = Open(null, []);

        Find<CheckBox>(window, "pushTag").Content.Should().Be("Push tag to 'origin'");
        Find<CommitPickerView>(window, "commitPickerSmallControl1").Selected.ToString().Should()
            .Be(_repo.Run("rev-parse", "HEAD").Trim(), "without a commit the current checkout is tagged");
        Find<ComboBox>(window, "annotate").SelectedIndex = 3;
        Find<TextBox>(window, "textBoxGpgKey").IsEnabled.Should().BeTrue();
        Find<TextBlock>(window, "keyIdLbl").IsEnabled.Should().BeTrue();
        owner.Close();
    }

    [AvaloniaTest]
    public void A_missing_name_shows_upstreams_error_and_the_window_stays()
    {
        (Window owner, CreateTagWindow window) = Open("HEAD", []);

        Click(window, "Ok");
        WaitUntil(() => window.OwnedWindows.OfType<MessageBoxWindow>().Any());
        MessageBoxWindow error = window.OwnedWindows.OfType<MessageBoxWindow>().Single();
        error.Title.Should().Be("Error");
        error.Close();
        window.IsVisible.Should().BeTrue();
        _repo.Run("tag", "--list").Should().BeEmpty();
        owner.Close();
    }

    private (Window Owner, CreateTagWindow Window) Open(string? revision, List<(string Remote, string Tag)> pushed)
    {
        Window owner = new();
        owner.Show();
        CreateTagWindow window = new(_repo.Path, revision,
            arguments => Task.FromResult(GitProcessRun(arguments)),
            (remote, tag) =>
            {
                pushed.Add((remote, tag));
                return Task.FromResult(true);
            });
        _ = window.ShowDialog(owner);
        WaitUntil(() => window.TagLoaded.IsCompleted);
        return (owner, window);
    }

    // The window's git command line, run in the test repository as the browse window's output panel would.
    private bool GitProcessRun(string arguments)
    {
        using System.Diagnostics.Process process = System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo("git", arguments) { WorkingDirectory = _repo.Path })!;
        process.WaitForExit();
        return process.ExitCode == 0;
    }

    private static void Click(Window window, string name)
        => Find<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static T Find<T>(Window window, string name)
        where T : Control
        => window.FindControl<T>(name)
           ?? window.GetLogicalDescendants().OfType<T>().FirstOrDefault(control => control.Name == name)
           ?? throw new InvalidOperationException($"No control named {name}");

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
