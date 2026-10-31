using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class RenameBranchWindowTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private TestRepository _repo = null!;
    private bool _autoNormalise;
    private string _symbol = "";

    [SetUp]
    public void SetUp()
    {
        _repo = new TestRepository();
        _repo.Run("branch", "feature");
        _autoNormalise = TestAppBuilder.Preferences.AutoNormaliseBranchName;
        _symbol = TestAppBuilder.Preferences.AutoNormaliseSymbol;
    }

    [TearDown]
    public void TearDown()
    {
        TestAppBuilder.Preferences.AutoNormaliseBranchName = _autoNormalise;
        TestAppBuilder.Preferences.AutoNormaliseSymbol = _symbol;
        _repo.Dispose();
    }

    [AvaloniaTest]
    public void Rename_normalises_the_name_and_runs_upstreams_command()
    {
        TestAppBuilder.Preferences.AutoNormaliseBranchName = true;
        TestAppBuilder.Preferences.AutoNormaliseSymbol = "_";
        List<string> commands = [];
        (Window owner, RenameBranchWindow window) = Open(commands);
        window.FindControl<TextBox>("BranchNameTextBox")!.Text.Should().Be("feature");

        window.FindControl<TextBox>("BranchNameTextBox")!.Text = "new name";
        Click(window, "Ok");

        WaitUntil(() => !window.IsVisible);
        commands.Should().Equal(GitCommands.Git.Commands.RenameBranch("feature", "new_name").ToString());
        _repo.Run("branch", "--list", "new_name").Should().Contain("new_name");
        owner.Close();
    }

    [AvaloniaTest]
    public void The_same_name_closes_without_renaming()
    {
        List<string> commands = [];
        (Window owner, RenameBranchWindow window) = Open(commands);

        Click(window, "Ok");

        WaitUntil(() => !window.IsVisible);
        commands.Should().BeEmpty();
        owner.Close();
    }

    [AvaloniaTest]
    public void The_left_panel_opens_the_window_on_the_branch()
    {
        MainWindow main = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        main.Show();
        main.OpenRepository(_repo.Path);
        WaitUntil(() => !main.IsBusy);
        TreeView tree = main.FindControl<TreeView>("BranchTreeView")!;
        WaitUntil(() => FindNode(tree, "feature") is not null);
        tree.SelectedItem = FindNode(tree, "feature");

        main.FindControl<MenuItem>("TreeRenameBranchMenuItem")!.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        WaitUntil(() => main.OwnedWindows.OfType<RenameBranchWindow>().Any());
        RenameBranchWindow window = main.OwnedWindows.OfType<RenameBranchWindow>().Single();
        window.FindControl<TextBox>("BranchNameTextBox")!.Text.Should().Be("feature");
        window.Close();
        main.Close();
    }

    private (Window Owner, RenameBranchWindow Window) Open(List<string> commands)
    {
        Window owner = new();
        owner.Show();
        RenameBranchWindow window = new("feature", TestAppBuilder.Preferences, arguments =>
        {
            commands.Add(arguments);
            using Process process = Process.Start(new ProcessStartInfo("git", arguments) { WorkingDirectory = _repo.Path })
                                    ?? throw new InvalidOperationException("git did not start");
            process.WaitForExit();
            return Task.FromResult(process.ExitCode == 0);
        });
        _ = window.ShowDialog(owner);
        Dispatcher.UIThread.RunJobs();
        return (owner, window);
    }

    private static RepoObjectNode? FindNode(TreeView tree, string name)
        => (tree.ItemsSource as IReadOnlyList<RepoObjectNode> ?? []).SelectMany(root => root.Descendants())
            .FirstOrDefault(node => node.Branch?.Name == name);

    private static void Click(Window window, string name)
        => window.FindControl<Button>(name)!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

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
