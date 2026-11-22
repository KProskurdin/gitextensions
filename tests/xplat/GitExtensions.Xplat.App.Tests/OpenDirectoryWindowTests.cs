using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using GitCommands;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

// Upstream's FormOpenDirectory, from the Start menu's "Open...".
internal sealed class OpenDirectoryWindowTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    private TestRepository _first = null!;
    private TestRepository _second = null!;

    [SetUp]
    public void Setup()
    {
        _first = new TestRepository();
        _second = new TestRepository();
    }

    [TearDown]
    public void TearDown()
    {
        _first.Dispose();
        _second.Dispose();
    }

    [AvaloniaTest]
    public void Open_lists_the_open_repositorys_folder_and_opens_the_repository_typed()
    {
        MainWindow window = OpenMainWindow(_first.Path);
        OpenDirectoryWindow open = OpenDialog(window);

        open.Title.Should().Be("Open local repository");
        open.ViewModel.Directories.Should().Contain(
            Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(_first.Path)) + Path.DirectorySeparatorChar);
        open.FindControl<Button>("Load")!.IsDefault.Should().BeTrue();

        open.ViewModel.Directory = _second.Path;
        UiClicks.Click(open.FindControl<Button>("Load")!);

        WaitUntil(() => !open.IsVisible && window.OpenRepositoryPath == Path.TrimEndingDirectorySeparator(_second.Path));
        window.Close();
    }

    [AvaloniaTest]
    public void Open_says_a_folder_that_is_no_repository_is_not_valid_and_stays_open()
    {
        MainWindow window = OpenMainWindow(_first.Path);
        OpenDirectoryWindow open = OpenDialog(window);
        string notRepository = Directory.CreateTempSubdirectory("xplat-not-repo-").FullName;
        try
        {
            open.ViewModel.Directory = notRepository;
            UiClicks.Click(open.FindControl<Button>("Load")!);

            MessageBoxWindow message = (MessageBoxWindow)WaitForOwned(open, typeof(MessageBoxWindow));
            message.Close();
            open.IsVisible.Should().BeTrue();
            window.OpenRepositoryPath.Should().Be(Path.TrimEndingDirectorySeparator(_first.Path));
            open.Close();
            window.Close();
        }
        finally
        {
            Directory.Delete(notRepository);
        }
    }

    private static MainWindow OpenMainWindow(string repository)
    {
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        window.OpenRepository(repository);
        WaitUntil(() => !window.IsBusy && window.OpenRepositoryPath is not null);
        return window;
    }

    private static OpenDirectoryWindow OpenDialog(MainWindow window)
    {
        UiClicks.Click(window.FindControl<MenuItem>("OpenMenuItem")!);
        return (OpenDirectoryWindow)WaitForOwned(window, typeof(OpenDirectoryWindow));
    }

    private static Window WaitForOwned(Window host, Type type)
    {
        WaitUntil(() => host.OwnedWindows.Any(window => window.GetType() == type && window.IsVisible));
        return host.OwnedWindows.Last(window => window.GetType() == type);
    }

    private static void WaitUntil(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow + _timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("The condition was not met in time.");
            }

            Dispatcher.UIThread.RunJobs();
        }
    }
}
