using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using GitCommands;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class SparseWorkingCopyWindowTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private TestRepository _repo = null!;
    private string _rulesFile = "";

    [SetUp]
    public void SetUp()
    {
        _repo = new TestRepository();
        _rulesFile = Path.Combine(_repo.Path, ".git", "info", "sparse-checkout");
    }

    [TearDown]
    public void TearDown() => _repo.Dispose();

    [AvaloniaTest]
    public void Enable_and_Save_write_the_setting_and_rules_and_refresh_the_working_copy()
    {
        List<string> commands = [];
        (Window owner, SparseWorkingCopyWindow window) = Open(commands);
        Find<Control>(window, "DisabledPanel").IsVisible.Should().BeTrue();
        Find<Control>(window, "RulesPanel").IsVisible.Should().BeFalse();

        Click(window, "EnableButton");
        Find<Control>(window, "RulesPanel").IsVisible.Should().BeTrue();
        Find<Control>(window, "DisabledPanel").IsVisible.Should().BeFalse();
        Find<TextBox>(window, "RulesEditor").Text = "/a.txt";
        Dispatcher.UIThread.RunJobs();
        Click(window, "SaveButton");

        WaitUntil(() => !window.IsVisible);
        _repo.Run("config", "--local", "core.sparsecheckout").Trim().Should().Be("true");
        File.ReadAllText(_rulesFile).Should().Be("/a.txt");
        commands.Should().Equal("read-tree -m -u HEAD");
        owner.Close();
    }

    [AvaloniaTest]
    public void Closing_with_changes_asks_and_No_closes_without_saving()
    {
        (Window owner, SparseWorkingCopyWindow window) = Open([]);
        Click(window, "EnableButton");

        window.Close();
        Answer(window, "CancelButton");
        window.IsVisible.Should().BeTrue("Cancel keeps the window open");

        window.Close();
        Answer(window, "NoButton");
        WaitUntil(() => !window.IsVisible);
        _repo.Run("config", "--local", "--list").Should().NotContain("core.sparsecheckout");
        owner.Close();
    }

    [AvaloniaTest]
    public void Disabling_with_rules_offers_to_let_the_whole_working_copy_through()
    {
        _repo.Run("config", "core.sparsecheckout", "true");
        Directory.CreateDirectory(Path.GetDirectoryName(_rulesFile)!);
        File.WriteAllText(_rulesFile, "/a.txt\n");
        (Window owner, SparseWorkingCopyWindow window) = Open([]);
        WaitUntil(() => window.RulesLoaded.IsCompleted);
        Find<TextBox>(window, "RulesEditor").Text.Should().Be("/a.txt\n");

        Find<HyperlinkButton>(window, "DisableLink").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Find<Control>(window, "DisabledPanel").IsVisible.Should().BeTrue();
        Click(window, "SaveButton");
        Answer(window, "YesButton");

        WaitUntil(() => !window.IsVisible);
        _repo.Run("config", "--local", "core.sparsecheckout").Trim().Should().Be("false");
        File.ReadAllText(_rulesFile).Should().Be(string.Join(Environment.NewLine, "/*", "#/a.txt"));
        owner.Close();
    }

    [AvaloniaTest]
    public void The_repository_menu_opens_the_window()
    {
        MainWindow main = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        main.Show();
        main.OpenRepository(_repo.Path);
        WaitUntil(() => !main.IsBusy);

        main.FindControl<MenuItem>("SparseWorkingCopyMenuItem")!.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        WaitUntil(() => main.OwnedWindows.OfType<SparseWorkingCopyWindow>().Any());
        main.OwnedWindows.OfType<SparseWorkingCopyWindow>().Single().Close();
        main.Close();
    }

    private (Window Owner, SparseWorkingCopyWindow Window) Open(List<string> commands)
    {
        Window owner = new();
        owner.Show();
        SparseWorkingCopyWindow window = new(_repo.Path, arguments =>
        {
            commands.Add(arguments);
            return Task.FromResult(true);
        });
        _ = window.ShowDialog(owner);
        WaitUntil(() => window.RulesLoaded.IsCompleted);
        return (owner, window);
    }

    private static void Answer(Window window, string button)
    {
        WaitUntil(() => window.OwnedWindows.OfType<MessageBoxWindow>().Any());
        MessageBoxWindow box = window.OwnedWindows.OfType<MessageBoxWindow>().Single();
        Click(box, button);
        WaitUntil(() => !box.IsVisible);
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
