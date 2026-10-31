using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class SubmodulesWindowTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private TestRepository _repo = null!;
    private TestRepository _library = null!;

    [SetUp]
    public void SetUp()
    {
        _library = new TestRepository();
        _repo = new TestRepository();
        _repo.Run("-c", "protocol.file.allow=always", "submodule", "add", "-q", _library.Path, "lib");
        _repo.Run("commit", "-q", "-m", "add submodule");
    }

    [TearDown]
    public void TearDown()
    {
        _repo.Dispose();
        _library.Dispose();
    }

    [AvaloniaTest]
    public void The_window_lists_the_submodules_and_shows_the_selected_ones_details()
    {
        (Window owner, SubmodulesWindow window) = Open();

        GitSubmoduleInfo lib = window.Items.Should().ContainSingle().Subject;
        lib.Name.Should().Be("lib");
        Find<TextBox>(window, "SubModuleName").Text.Should().Be("lib", "the first submodule is selected");
        Find<TextBox>(window, "SubModuleLocalPath").Text.Should().Be("lib");
        Find<TextBox>(window, "SubModuleRemotePath").Text.Should()
            .Be(_repo.Run("config", "-f", ".gitmodules", "submodule.lib.url").Trim());
        Find<TextBox>(window, "SubModuleCommit").Text.Should().Be(_library.Run("rev-parse", "HEAD").Trim());
        Find<TextBox>(window, "SubModuleStatus").Text.Should().Be("Up-to-date");
        owner.Close();
    }

    [AvaloniaTest]
    public void Synchronize_update_and_pull_act_on_the_selected_submodule()
    {
        List<string> commands = [];
        List<string> pulled = [];
        (Window owner, SubmodulesWindow window) = Open(
            arguments =>
            {
                commands.Add(arguments);
                return Task.FromResult(true);
            },
            (_, path) =>
            {
                pulled.Add(path);
                return Task.CompletedTask;
            });

        Click(window, "SynchronizeSubmodule");
        WaitUntil(() => commands.Count == 1);
        Click(window, "UpdateSubmodule");
        WaitUntil(() => commands.Count == 2);
        Click(window, "Pull");
        WaitUntil(() => pulled.Count == 1);

        commands.Should().Equal(
            GitCommands.Git.Commands.SubmoduleSync("lib").ToString(),
            GitCommands.Git.Commands.SubmoduleUpdate("lib").ToString());
        pulled.Should().Equal(Path.Combine(_repo.Path, "lib"));
        owner.Close();
    }

    [AvaloniaTest]
    public void Remove_takes_the_submodule_out_of_the_index_and_configuration_after_the_question()
    {
        (Window owner, SubmodulesWindow window) = Open();

        Click(window, "RemoveSubmodule");
        Answer(window, "NoButton");
        _repo.Run("ls-files", "--stage", "lib").Should().NotBeEmpty();

        Click(window, "RemoveSubmodule");
        Answer(window, "YesButton");
        WaitUntil(() => window.SubmodulesLoaded.IsCompleted && window.Items.Count == 0);
        _repo.Run("ls-files", "--stage", "lib").Should().BeEmpty();
        _repo.Run("ls-files", "--stage", ".gitmodules").Should().BeEmpty("no submodule is left, so .gitmodules is unstaged");
        _repo.Run("config", "--local", "--list").Should().NotContain("submodule.lib");
        Directory.Exists(Path.Combine(_repo.Path, "lib")).Should().BeTrue("upstream leaves the folder");
        owner.Close();
    }

    [AvaloniaTest]
    public void Add_proposes_the_local_path_needs_both_paths_and_runs_upstreams_command()
    {
        Window owner = new();
        owner.Show();
        List<string> commands = [];
        AddSubmoduleWindow add = new(_repo.Path, arguments =>
        {
            commands.Add(arguments);
            return Task.FromResult(true);
        });
        _ = add.ShowDialog(owner);

        Click(add, "Add");
        Answer(add, "OKButton");
        commands.Should().BeEmpty();

        Find<ComboBox>(add, "Directory").Text = "https://example.com/team/tools.git";
        Find<TextBox>(add, "LocalPath").Text.Should().Be("tools");
        Find<ComboBox>(add, "Branch").Text = "main";
        Find<CheckBox>(add, "chkForce").IsChecked = true;
        Click(add, "Add");

        WaitUntil(() => !add.IsVisible);
        commands.Should().Equal(
            SubmodulesDialog.AddArguments("https://example.com/team/tools.git", "tools", "main", force: true).ToString());
        owner.Close();
    }

    [AvaloniaTest]
    public void LoadRemoteBranches_lists_the_remotes_heads()
    {
        string branch = _library.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();
        _library.Run("branch", "feature");
        IReadOnlyList<string> branches = [];
        Task load = Task.Run(() =>
            branches = SubmodulesDialog.LoadRemoteBranches(GitModules.Open(_repo.Path).GitExecutable, _library.Path));
        WaitUntil(() => load.IsCompleted);

        branches.Should().BeEquivalentTo(branch, "feature");
        SubmodulesDialog.LoadRemoteBranches(GitModules.Open(_repo.Path).GitExecutable, " ").Should().BeEmpty();
    }

    [AvaloniaTest]
    public void The_repository_menu_opens_the_window()
    {
        MainWindow main = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        main.Show();
        main.OpenRepository(_repo.Path);
        WaitUntil(() => !main.IsBusy);
        main.FindControl<MenuItem>("ManageSubmodulesMenuItem")!.IsEnabled.Should().BeTrue();

        main.FindControl<MenuItem>("ManageSubmodulesMenuItem")!.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        WaitUntil(() => main.OwnedWindows.OfType<SubmodulesWindow>().Any());
        SubmodulesWindow window = main.OwnedWindows.OfType<SubmodulesWindow>().Single();
        WaitUntil(() => window.Items.Count == 1);
        window.Close();
        main.Close();
    }

    private (Window Owner, SubmodulesWindow Window) Open(Func<string, Task<bool>>? runGit = null,
        Func<Window, string, Task>? pull = null)
    {
        Window owner = new();
        owner.Show();
        SubmodulesWindow window = new(_repo.Path, runGit ?? (_ => Task.FromResult(true)),
            pull ?? ((_, _) => Task.CompletedTask));
        _ = window.ShowDialog(owner);
        WaitUntil(() => window.Items.Count > 0 && window.SubmodulesLoaded.IsCompleted);
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
