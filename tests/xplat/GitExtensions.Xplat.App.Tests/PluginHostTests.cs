using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Extensibility.Settings;
using GitExtensions.Xplat.Core.Plugins;
using GitExtensions.Xplat.Core.Scripts;
using GitExtensions.Xplat.Ui;
using GitUI.ScriptsEngine;
using GitUIPluginInterfaces;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class PluginHostTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    private TestRepository _repo = null!;
    private RecordingPlugin _plugin = null!;

    [SetUp]
    public void Setup()
    {
        _repo = new TestRepository();
        _plugin = new RecordingPlugin();
        TestAppBuilder.UsePlugins(_plugin);
    }

    [TearDown]
    public void TearDown()
    {
        TestAppBuilder.UsePlugins();
        TestAppBuilder.Scripts.Save([]);
        ((IDisposable)_plugin).Dispose();
        _repo.Dispose();
    }

    [AvaloniaTest]
    public void The_plugins_menu_lists_the_plugins_and_runs_them_on_the_open_repository()
    {
        MainWindow window = NewWindow();
        window.Show();
        MenuItem item = WaitForPluginItem(window);

        // A repository plugin is off on the dashboard, as upstream's UpdatePluginMenu turns it off.
        item.IsEnabled.Should().BeFalse();
        Open(window, _repo.Path);
        item.IsEnabled.Should().BeTrue();
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

        _plugin.ExecutedIn.Should().ContainSingle().Which.Should().Be(_repo.Path);
    }

    [AvaloniaTest]
    public void Plugins_move_with_the_repository_and_are_unregistered_when_the_window_closes()
    {
        MainWindow window = NewWindow();
        window.Show();
        WaitForPluginItem(window);
        Open(window, _repo.Path);
        Click(window, "CloseRepositoryMenuItem");
        window.Close();

        // As upstream's SetGitModule: unregistered from the dashboard before the repository, and back to the dashboard.
        _plugin.Calls.Should().Equal(
            "register ",
            "unregister ",
            $"register {_repo.Path}",
            $"unregister {_repo.Path}",
            "register ",
            "unregister ");
    }

    [AvaloniaTest]
    public void A_plugin_can_stop_the_commit_window_from_opening_and_hears_when_it_closes()
    {
        MainWindow window = NewWindow();
        window.Show();
        WaitForPluginItem(window);
        Open(window, _repo.Path);
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "changed");
        WaitUntil(() => Find<Button>(window, "CommitDialogButton").IsEnabled);

        _plugin.CancelCommit = true;
        Click(window, "CommitDialogButton");
        Dispatcher.UIThread.RunJobs();
        window.OwnedWindows.OfType<CommitWindow>().Should().BeEmpty();

        _plugin.CancelCommit = false;
        Click(window, "CommitDialogButton");
        WaitUntil(() => window.OwnedWindows.OfType<CommitWindow>().Any());
        window.OwnedWindows.OfType<CommitWindow>().Single().Close();

        _plugin.Calls.Should().ContainInOrder("pre commit", "pre commit", "post commit True");
    }

    [AvaloniaTest]
    public void Plugin_settings_are_edited_on_the_plugins_tab_and_the_plugin_hears_about_it()
    {
        MainWindow window = NewWindow();
        window.Show();
        WaitForPluginItem(window);

        Click(window, "PluginSettingsMenuItem");
        WaitUntil(() => window.OwnedWindows.OfType<SettingsWindow>().Any());
        SettingsWindow settings = window.OwnedWindows.OfType<SettingsWindow>().Single();
        WaitUntil(() => settings.IsGitConfigLoaded);
        Find<TabControl>(settings, "Tabs").SelectedItem.Should().Be(Find<TabItem>(settings, "PluginsTab"));
        settings.SelectedPluginEditor!.Title.Should().Be(RecordingPlugin.PluginName);
        TextBox arguments = Find<StackPanel>(settings, "PluginSettingsPanel").GetLogicalDescendants()
            .OfType<TextBox>().First();
        arguments.Text = "fetch --prune";
        CheckBox enabled = Find<StackPanel>(settings, "PluginSettingsPanel").GetLogicalDescendants()
            .OfType<CheckBox>().Single();
        enabled.IsChecked = true;
        Click(settings, "SaveButton");
        WaitUntil(() => !window.OwnedWindows.OfType<SettingsWindow>().Any());

        TestAppBuilder.PluginSettings.Values.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            [$"{RecordingPlugin.PluginId}.Arguments"] = "fetch --prune",
            [$"{RecordingPlugin.PluginId}.Enabled"] = "true",
        });
        WaitUntil(() => _plugin.Calls.Contains("post settings True"));
    }

    [AvaloniaTest]
    public void A_plugin_asking_for_a_dialog_the_app_does_not_have_gets_an_error_not_a_crash()
    {
        MainWindow window = NewWindow();
        window.Show();
        MenuItem item = WaitForPluginItem(window);
        Open(window, _repo.Path);
        _plugin.OnExecute = args => args.GitUICommands.StartPushDialog(args.OwnerForm, pushOnShow: true);

        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

        WaitUntil(() => window.OwnedWindows.OfType<ErrorWindow>().Any());
    }

    [AvaloniaTest]
    public void A_change_notice_from_a_background_thread_reaches_the_plugins_on_the_UI_thread()
    {
        MainWindow window = NewWindow();
        window.Show();
        WaitForPluginItem(window);
        Open(window, _repo.Path);

        Thread notifier = new(() => _plugin.Commands!.RepoChangedNotifier.Notify());
        notifier.Start();
        notifier.Join();

        WaitUntil(() => _plugin.Calls.Contains("repository changed on UI thread"));
    }

    [AvaloniaTest]
    public void Upstreams_plugins_load_with_upstreams_host_from_the_plugins_folder()
    {
        // The plugins built for the new shell are copied into Plugins next to the app, and so next to these tests.
        string[] names = ["GitExtensions.Plugins.BackgroundFetch", "GitExtensions.Plugins.AutoCompileSubmodules"];
        ManagedExtensibility.Initialise([
            .. names.Select(name =>
                Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory, "Plugins", name, name + ".dll")))
        ]);

        IReadOnlyList<IGitPlugin> plugins = PluginLoader.Load(ManagedExtensibility.GetExports<IGitPlugin>());

        // Each constructs: what they use of the shared core (e.g. ResourceManager's TranslationString) ships with the app.
        plugins.Should().NotContain(plugin => plugin is FailedPlugin);
        plugins.Should().Contain(plugin => plugin.Name == "Auto compile submodules");
        IGitPlugin fetch = plugins.Should().ContainSingle(plugin => plugin.Name == "Periodic background fetch").Subject;
        fetch.Should().BeAssignableTo<IGitPluginForRepository>();
        fetch.GetType().Assembly.GetManifestResourceNames().Should().Contain("GitExtensions.Xplat.PluginIcon.png");
        new PluginSettingsEditor(fetch, new InMemorySettingsSource()).Rows.Select(row => row.Kind).Should().Equal(
            PluginSettingKind.Text,
            PluginSettingKind.Number,
            PluginSettingKind.Bool,
            PluginSettingKind.Bool,
            PluginSettingKind.Bool,
            PluginSettingKind.Note);
    }

    [AvaloniaTest]
    public void A_script_with_a_plugin_command_runs_the_plugin()
    {
        TestAppBuilder.Scripts.Save(
        [
            new ScriptDefinition
            {
                Name = "Record", Command = "{plugin:RECORDER}", OnEvent = ScriptEvent.ShowInUserMenuBar, Enabled = true,
            },
        ]);
        MainWindow window = NewWindow();
        window.Show();
        WaitForPluginItem(window);
        Open(window, _repo.Path);
        StackPanel scripts = Find<StackPanel>(window, "UserScriptsPanel");

        scripts.Children.OfType<Button>().Single().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        WaitUntil(() => _plugin.ExecutedIn.Count == 1);
        _plugin.ExecutedIn.Should().Equal(_repo.Path);
    }

    [AvaloniaTest]
    public void An_upstream_message_box_waits_for_the_answer_on_the_UI_thread_and_from_a_background_thread()
    {
        Window owner = new();
        owner.Show();
        List<string> seen = [];

        // A safety net: a box still open after the timeout is closed, so a failure cannot hang the test run.
        DispatcherTimer.RunOnce(() =>
        {
            seen.Add("timeout");
            foreach (MessageBoxWindow open in owner.OwnedWindows.OfType<MessageBoxWindow>().ToList())
            {
                open.Close();
            }
        }, _timeout);
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                MessageBoxWindow box = owner.OwnedWindows.OfType<MessageBoxWindow>().Single();
                seen.Add(box.Title!);
                box.GetLogicalDescendants().OfType<Button>().Single(button => button.Name == "NoButton")
                    .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            catch (Exception ex)
            {
                seen.Add(ex.Message);
            }
        });

        System.Windows.Forms.DialogResult onUiThread = MessageBoxHost.Show(new WindowOwner(owner), "Build it?", "Build",
            System.Windows.Forms.MessageBoxButtons.YesNoCancel, System.Windows.Forms.MessageBoxIcon.Question,
            System.Windows.Forms.MessageBoxDefaultButton.Button1);

        seen.Should().Equal("Build");
        onUiThread.Should().Be(System.Windows.Forms.DialogResult.No);

        // From a background thread the box opens on the UI thread; the timer closes it from inside the box's frame.
        DispatcherTimer closer = new() { Interval = TimeSpan.FromMilliseconds(50) };
        closer.Tick += (_, _) =>
        {
            foreach (MessageBoxWindow open in owner.OwnedWindows.OfType<MessageBoxWindow>().ToList())
            {
                seen.Add(open.Title!);
                open.Close();
            }
        };
        closer.Start();
        owner.Activate();
        Task<System.Windows.Forms.DialogResult> fromBackground = Task.Run(() => MessageBoxHost.Show(
            new WindowOwner(owner), "Failed", "", System.Windows.Forms.MessageBoxButtons.OK,
            System.Windows.Forms.MessageBoxIcon.Error, System.Windows.Forms.MessageBoxDefaultButton.Button1));
        WaitUntil(() => fromBackground.IsCompleted);
        closer.Stop();

        // As WinForms: closing a box that has only OK answers OK.
        seen.Should().Equal("Build", "Git Extensions");
        fromBackground.Result.Should().Be(System.Windows.Forms.DialogResult.OK);
    }

    private static MainWindow NewWindow() =>
        new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));

    private static MenuItem WaitForPluginItem(MainWindow window)
    {
        MenuItem menu = Find<MenuItem>(window, "PluginsMenu");
        WaitUntil(() => menu.Items.OfType<MenuItem>().Any(item => item.Tag is RecordingPlugin));
        return menu.Items.OfType<MenuItem>().Single(item => item.Tag is RecordingPlugin);
    }

    private static void Open(MainWindow window, string path)
    {
        Find<TextBox>(window, "PathBox").Text = path;
        Find<Button>(window, "OpenButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        WaitUntil(() => Find<Button>(window, "OpenButton").IsEnabled);
    }

    private static void Click(Window window, string name)
    {
        Control control = Find<Control>(window, name);
        RoutedEvent click = control is MenuItem ? MenuItem.ClickEvent : Button.ClickEvent;
        control.RaiseEvent(new RoutedEventArgs(click));
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

    /// <summary>
    ///  Records what the host does with a plugin, in the order it happens.
    /// </summary>
    private sealed class RecordingPlugin : GitPluginBase, IGitPluginForRepository
    {
        public const string PluginName = "Recorder";

        public static readonly Guid PluginId = new("0B7F5E7E-6A55-4B0B-9D2C-3C2B9E1A7F10");

        private readonly StringSetting _arguments = new("Arguments", "fetch --all");
        private readonly BoolSetting _enabled = new("Enabled", false);

        public RecordingPlugin()
            : base(hasSettings: true)
        {
            Id = PluginId;
            Name = PluginName;
            Description = PluginName;
        }

        public List<string> Calls { get; } = [];

        public List<string> ExecutedIn { get; } = [];

        public bool CancelCommit { get; set; }

        public Func<GitUIEventArgs, bool>? OnExecute { get; set; }

        public IGitUICommands? Commands { get; private set; }

        public override IEnumerable<ISetting> GetSettings() => [_arguments, _enabled];

        public override void Register(IGitUICommands gitUiCommands)
        {
            Commands = gitUiCommands;
            Calls.Add($"register {Folder(gitUiCommands.Module)}");
            gitUiCommands.PreCommit += OnPreCommit;
            gitUiCommands.PostCommit += OnPostCommit;
            gitUiCommands.PostSettings += OnPostSettings;
            gitUiCommands.PostRepositoryChanged += OnRepositoryChanged;
        }

        public override void Unregister(IGitUICommands gitUiCommands)
        {
            Calls.Add($"unregister {Folder(gitUiCommands.Module)}");
            gitUiCommands.PreCommit -= OnPreCommit;
            gitUiCommands.PostCommit -= OnPostCommit;
            gitUiCommands.PostSettings -= OnPostSettings;
            gitUiCommands.PostRepositoryChanged -= OnRepositoryChanged;
        }

        public override bool Execute(GitUIEventArgs args)
        {
            ExecutedIn.Add(Folder(args.GitModule));
            return OnExecute?.Invoke(args) ?? false;
        }

        // The module's folder as the tests name it: no trailing separator, empty for the dashboard.
        private static string Folder(IGitModule module)
            => module.WorkingDir.Length == 0 ? "" : Path.TrimEndingDirectorySeparator(module.WorkingDir);

        private void OnPreCommit(object? sender, GitUIEventArgs e)
        {
            Calls.Add("pre commit");
            e.Cancel = CancelCommit;
        }

        private void OnPostCommit(object? sender, GitUIPostActionEventArgs e) =>
            Calls.Add($"post commit {e.ActionDone}");

        private void OnPostSettings(object? sender, GitUIPostActionEventArgs e) =>
            Calls.Add($"post settings {e.ActionDone}");

        private void OnRepositoryChanged(object? sender, GitUIEventArgs e)
            => Calls.Add(Dispatcher.UIThread.CheckAccess() ? "repository changed on UI thread" : "repository changed");
    }
}
