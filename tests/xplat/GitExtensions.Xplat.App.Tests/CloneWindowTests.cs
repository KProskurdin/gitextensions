using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Xplat.App;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class CloneWindowTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    [AvaloniaTest]
    public void Clone_returns_the_entered_url_and_target_folder()
    {
        MainWindow owner = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        owner.Show();
        CloneWindow dialog = new();
        Task<CloneRequest?> result = dialog.ShowDialog<CloneRequest?>(owner);
        Find<TextBox>(dialog, "UrlBox").Text = " /srv/repo.git ";
        Find<TextBox>(dialog, "TargetBox").Text = "/work/repo";

        Click(dialog, "CloneButton");
        WaitUntil(() => result.IsCompleted);

        result.Result.Should().Be(new CloneRequest("/srv/repo.git", "/work/repo"));
        owner.Close();
    }

    [AvaloniaTest]
    public void Clone_without_a_target_folder_keeps_the_dialog_open()
    {
        MainWindow owner = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        owner.Show();
        CloneWindow dialog = new();
        Task<CloneRequest?> result = dialog.ShowDialog<CloneRequest?>(owner);
        Find<TextBox>(dialog, "UrlBox").Text = "/srv/repo.git";

        Click(dialog, "CloneButton");
        Dispatcher.UIThread.RunJobs();

        result.IsCompleted.Should().BeFalse();
        Click(dialog, "CancelButton");
        WaitUntil(() => result.IsCompleted);
        owner.Close();
    }

    [AvaloniaTest]
    public void Cancel_returns_no_request()
    {
        MainWindow owner = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        owner.Show();
        CloneWindow dialog = new();
        Task<CloneRequest?> result = dialog.ShowDialog<CloneRequest?>(owner);
        Find<TextBox>(dialog, "UrlBox").Text = "/srv/repo.git";
        Find<TextBox>(dialog, "TargetBox").Text = "/work/repo";

        Click(dialog, "CancelButton");
        WaitUntil(() => result.IsCompleted);

        result.Result.Should().BeNull();
        owner.Close();
    }

    private static void Click(Window window, string buttonName)
        => Find<Button>(window, buttonName).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static T Find<T>(Window window, string name) where T : Control
        => window.FindControl<T>(name) ?? throw new InvalidOperationException($"Control {name} not found");

    private static void WaitUntil(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow + _timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("Timed out waiting for the dialog");
            }

            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(20);
        }
    }
}
