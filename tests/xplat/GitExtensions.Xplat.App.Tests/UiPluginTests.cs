using System.ComponentModel.Composition;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Extensibility.Settings;
using GitUI;
using NUnit.Framework;
using DialogResult = System.Windows.Forms.DialogResult;

namespace GitExtensions.Xplat.App.Tests;

// Upstream plugins with WinForms forms, built for the new shell (src/xplat/plugins): each plugin class is upstream's, loaded
// from the Plugins folder and run from the Plugins menu; its form is a stand-in that shows an Avalonia window modally.
internal sealed class UiPluginTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private TestRepository _repo = null!;
    private readonly List<string> _folders = [];
    private readonly List<string> _messages = [];

    [SetUp]
    public void Setup()
    {
        _repo = new TestRepository();
        _messages.Clear();

        // Upstream code shows its message boxes through the shim; the tests record them and answer Yes or OK.
        MessageBoxHost.Answer((_, text, _, buttons, _, _) =>
        {
            _messages.Add(text);
            return buttons is System.Windows.Forms.MessageBoxButtons.YesNo ? DialogResult.Yes : DialogResult.OK;
        });
    }

    [TearDown]
    public void TearDown()
    {
        AppSettings.CurrentTranslation = "";
        MessageBoxHost.Answer(null);
        TestAppBuilder.UsePlugins();
        _repo.Dispose();
        foreach (string folder in _folders)
        {
            GitProcess.DeleteFolder(folder);
        }
    }

    [AvaloniaTest]
    public void Create_local_branches_creates_a_tracking_branch_for_each_remote_branch()
    {
        string remote = NewFolder();
        GitProcess.Run(remote, "init", "--bare", "-q");
        _repo.Run("remote", "add", "origin", remote);
        _repo.Run("push", "-q", "origin", "HEAD:refs/heads/feature/one", "HEAD:refs/heads/two");
        _repo.Run("fetch", "-q", "origin");
        IGitPlugin plugin = LoadPlugin("CreateLocalBranches");
        MainWindow window = OpenWindow(plugin);

        RunPlugin(window, plugin, dialog =>
        {
            Find<TextBox>(dialog, "RemoteBox").Text.Should().Be("origin");
            Click(dialog, "button1");
            return true;
        });

        _repo.Run("for-each-ref", "--format=%(refname:short) %(upstream:short)", "refs/heads/").Should()
            .Contain("feature/one origin/feature/one").And.Contain("two origin/two");
        _messages.Should().ContainSingle().Which.Should().EndWith("local tracking branches have been created/updated.");
    }

    [AvaloniaTest]
    public void Plugin_windows_show_upstreams_translations_of_the_chosen_language()
    {
        AppSettings.CurrentTranslation = "German";
        IGitPlugin createBranches = LoadPlugin("CreateLocalBranches");
        MainWindow window = OpenWindow(createBranches);

        RunPlugin(window, createBranches, dialog =>
        {
            dialog.Title.Should().Be("Erzeuge lokale Branches zur Verfolgung");
            Find<TextBlock>(dialog, "label1").Text.Should().Be("Remote zu dem Tracking Branches erzeugt werden sollen");
            Find<Button>(dialog, "button1").Content.Should().Be("Erzeuge lokale Branches zur Verfolgung");
            return true;
        });

        // Texts the window's code shows come from the same entries: no proxy host set, so the window says so and closes.
        IGitPlugin proxySwitcher = LoadPlugin("ProxySwitcher");
        window = OpenWindow(proxySwitcher);
        TestSettings(proxySwitcher);
        RunPlugin(window, proxySwitcher, _ => false);

        _messages.Should().Equal(
            "Es ist kein Proxy konfiguriert. Bitte setzen Sie den Proxy-Host in den Plugin Einstellungen.");
    }

    [AvaloniaTest]
    public void The_browse_window_shows_upstreams_translations_of_its_texts()
    {
        AppSettings.CurrentTranslation = "German";
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        Dispatcher.UIThread.RunJobs();

        window.FindControl<Button>("CommitDialogButton")!.Content.Should().Be("Committen");
        window.FindControl<MenuItem>("ShowAuthorDateMenuItem")!.Header.Should().Be("Zeige Autor Datum");
        window.Close();
    }

    [AvaloniaTest]
    public void Texts_translate_but_the_items_of_a_data_list_do_not()
    {
        AppSettings.CurrentTranslation = "German";
        TextBlock label = new() { Text = "Commit" };
        ListBox list = new() { ItemsSource = new[] { "Commit" } };
        Window window = new() { Content = new StackPanel { Children = { label, list } } };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        label.Text.Should().Be("Committen");
        list.GetRealizedContainers().OfType<ListBoxItem>().Single().Content.Should().Be("Commit");
        window.Close();
    }

    [AvaloniaTest]
    public void Plugin_windows_keep_upstreams_english_texts_without_a_language()
    {
        IGitPlugin plugin = LoadPlugin("CreateLocalBranches");
        MainWindow window = OpenWindow(plugin);

        RunPlugin(window, plugin, dialog =>
        {
            dialog.Title.Should().Be("Create local tracking branches");
            Find<TextBlock>(dialog, "label1").Text.Should().Be("Remote to create tracking branches for");
            return true;
        });
    }

    [AvaloniaTest]
    public void Proxy_switcher_sets_and_unsets_the_repositorys_proxy_from_the_plugin_settings()
    {
        IGitPlugin plugin = LoadPlugin("ProxySwitcher");
        MainWindow window = OpenWindow(plugin);
        SettingsSource settings = TestSettings(plugin);
        settings.SetString("HTTP proxy", "proxy.example.com");
        settings.SetString("Username", "user");
        settings.SetString("Password", "secret");
        int step = 0;

        RunPlugin(window, plugin, dialog =>
        {
            // Never the user's global config: the local level only.
            Find<CheckBox>(dialog, "ApplyGlobally_CheckBox").IsChecked = false;
            switch (step++)
            {
                case 0:
                    Click(dialog, "SetProxy_Button");
                    _repo.Run("config", "--local", "http.proxy").Trim().Should()
                        .Be("user:secret@proxy.example.com:8080");
                    Find<TextBox>(dialog, "LocalProxyBox").Text.Should().Be("user:****@proxy.example.com:8080");
                    return false;
                default:
                    Click(dialog, "UnsetProxy_Button");
                    GitProcess.RunAllowingFailure(_repo.Path, "config", "--local", "http.proxy").Should().Be(1);
                    return true;
            }
        });

        _messages.Should().BeEmpty();
    }

    [AvaloniaTest]
    public void Proxy_switcher_says_when_no_proxy_is_configured_and_closes()
    {
        IGitPlugin plugin = LoadPlugin("ProxySwitcher");
        MainWindow window = OpenWindow(plugin);

        RunPlugin(window, plugin, _ => false);

        _messages.Should().Equal("There is no proxy configured. Please set the proxy host in the plugin settings.");
    }

    [AvaloniaTest]
    public void Find_large_files_writes_upstreams_batch_file_on_windows_and_the_same_steps_for_sh_elsewhere()
    {
        string original = AppSettings.GitCommandValue;
        try
        {
            AppSettings.GitCommandValue = @"C:\Program Files\Git\bin\git.exe";
            string[] paths =
            [
                "intune/packages/file.intunewin", "intune/packages/file with spaces.intunewin",
                "intune/packages/XL Upload/xl-upload.intunewin",
            ];

            // Upstream's own snapshots of FindLargeFilesForm.GenerateCommand.
            Script(paths, windows: true).Should().Be(UpstreamSnapshot("with_deletions"));
            Script([], windows: true).Should().Be(UpstreamSnapshot("without_deletions"));

            Script(["it's here/a b.bin"], windows: false).Should().Be(
                "gitexe='C:\\Program Files\\Git\\bin\\git.exe'\n" +
                "\"$gitexe\" filter-branch --index-filter 'git rm -r -f --cached --ignore-unmatch -- '\\''it'\\''\\'\\'''\\''s here/a b.bin'\\''' --prune-empty -- --all\n" +
                "\"$gitexe\" for-each-ref --format='%(refname)' refs/original/ | while read -r ref; do \"$gitexe\" update-ref -d \"$ref\"; done\n" +
                "\"$gitexe\" reflog expire --expire=now --all\n" +
                "\"$gitexe\" gc --aggressive --prune=now\n");
        }
        finally
        {
            AppSettings.GitCommandValue = original;
        }

        static string Script(string[] paths, bool windows)
            => ((string)PluginType("FindLargeFiles", "FindLargeFilesScript").GetMethod("Generate")!
                .Invoke(null, [paths, windows])!).ReplaceLineEndings("\n");

        static string UpstreamSnapshot(string name)
            => File.ReadAllText(Path.Combine(RepositoryRoot(), "tests", "plugins", "UnitTests", "FindLargeFiles.Tests",
                    $"FindLargeFilesFormTests.GenerateCommand_{name}_should_return_expected.verified.txt"))
                .TrimStart('\uFEFF').ReplaceLineEndings("\n").TrimEnd('\n') + "\n";
    }

    [AvaloniaTest]
    public void Find_large_files_lists_a_big_file_and_removes_it_from_the_history()
    {
        File.WriteAllBytes(Path.Combine(_repo.Path, "big.bin"), new byte[20_000]);
        _repo.Run("add", "big.bin");
        _repo.Run("commit", "-q", "-m", "big");
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "after");
        _repo.Run("commit", "-q", "-am", "after");
        IGitPlugin plugin = LoadPlugin("FindLargeFiles");
        MainWindow window = OpenWindow(plugin);
        // Upstream's NumberSetting parses with the current culture.
        TestSettings(plugin).SetString("Find large files bigger than (Mb)", 0.01f.ToString(CultureInfo.CurrentCulture));

        // git filter-branch otherwise waits 10 seconds after its warning.
        Environment.SetEnvironmentVariable("FILTER_BRANCH_SQUELCH_WARNING", "1");
        try
        {
            RunPlugin(window, plugin, dialog =>
            {
                if (Find<ProgressBar>(dialog, "ScanProgress").IsVisible)
                {
                    return false;
                }

                ListBox files = Find<ListBox>(dialog, "FileList");
                object row = files.Items.Cast<object>().Should().ContainSingle().Subject;
                row.GetType().GetProperty("Path")!.GetValue(row).Should().Be("big.bin");
                row.GetType().GetProperty("CommitCount")!.GetValue(row).Should().Be(2);
                row.GetType().GetProperty("Delete")!.SetValue(row, true);
                Click(dialog, "Delete");
                return true;
            });

            _messages.Should().Equal("Are you sure to delete the selected files?");
            ProcessWindow process = window.OwnedWindows.OfType<ProcessWindow>().Single();
            WaitUntil(() =>
                Find<TextBlock>(process, "StateText").Text is { } text
                && (text == "Done" || text.StartsWith("Failed", StringComparison.Ordinal)));
            Find<TextBlock>(process, "StateText").Text.Should().Be("Done");
            process.Close();
        }
        finally
        {
            Environment.SetEnvironmentVariable("FILTER_BRANCH_SQUELCH_WARNING", null);
        }

        _repo.Run("log", "--all", "--format=%s", "--", "big.bin").Trim().Should().BeEmpty();

        // "big" only added the file, so --prune-empty drops it.
        _repo.Run("log", "--format=%s").Split('\n', StringSplitOptions.RemoveEmptyEntries).Should()
            .Equal("after", "second", "first");
    }

    [AvaloniaTest]
    public void Delete_obsolete_branches_lists_the_merged_branches_and_deletes_the_ticked_ones()
    {
        _repo.Run("branch", "merged-one", "HEAD~1");
        _repo.Run("branch", "merged-two");
        _repo.Run("checkout", "-q", "-b", "unmerged");
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "unmerged");
        _repo.Run("commit", "-q", "-am", "unmerged");
        _repo.Run("checkout", "-q", "-");
        IGitPlugin plugin = LoadPlugin("DeleteUnusedBranches");
        MainWindow window = OpenWindow(plugin);

        // Every branch is old enough when the age limit is 0 days.
        TestSettings(plugin).SetString("Delete obsolete branches older than (days)", "0");
        bool deleteClicked = false;

        RunPlugin(window, plugin, dialog =>
        {
            ListBox branches = Find<ListBox>(dialog, "BranchList");
            if (!deleteClicked)
            {
                if (!Equals(Find<Button>(dialog, "RefreshBtn").Content, "Search branches") || branches.ItemCount == 0)
                {
                    return false;
                }

                branches.Items.Cast<object>().Select(row => row.GetType().GetProperty("Name")!.GetValue(row))
                    .Should().BeEquivalentTo(["merged-one", "merged-two"]);
                Find<TextBlock>(dialog, "StatusText").Text.Should().Be("2/2 branches selected.");
                Find<CheckBox>(dialog, "SelectAllCheck").IsChecked.Should().BeTrue();
                Click(dialog, "Delete");
                deleteClicked = true;
                return false;
            }

            // The list is searched again after the deletion, and the deleted branches are gone.
            return Equals(Find<Button>(dialog, "RefreshBtn").Content, "Search branches")
                   && Find<Button>(dialog, "Delete").IsEnabled && branches.ItemCount == 0;
        });

        _messages.Should().Equal("Are you sure to delete 2 selected branches?");
        _repo.Run("branch", "--format=%(refname:short)").Split('\n', StringSplitOptions.RemoveEmptyEntries).Should()
            .BeEquivalentTo([_repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim(), "unmerged"]);
    }

    [AvaloniaTest]
    public void Release_notes_generator_lists_the_range_and_copies_it_as_upstreams_text_table()
    {
        string hash = _repo.Run("log", "-1", "--format=%h").Trim();
        IGitPlugin plugin = LoadPlugin("ReleaseNotesGenerator");
        MainWindow window = OpenWindow(plugin);

        RunPlugin(window, plugin, dialog =>
        {
            Click(dialog, "buttonGenerate");
            Find<HeaderedContentControl>(dialog, "groupBoxCopy").IsEnabled.Should().BeFalse();

            Find<TextBox>(dialog, "FromBox").Text = "HEAD~1";
            Click(dialog, "buttonGenerate");
            Find<TextBox>(dialog, "ResultBox").Text.Should().Be($"{hash}@secondbody line{Environment.NewLine}");
            Find<TextBlock>(dialog, "labelRevCount").Text.Should().Be("1");
            Find<HeaderedContentControl>(dialog, "groupBoxCopy").IsEnabled.Should().BeTrue();

            Click(dialog, "buttonCopyAsTextTableTab");
            Copied(dialog).Should().Be(
                $"Commit log from 'HEAD~1' to 'HEAD' (most recent changes are listed on top):{Environment.NewLine}" +
                $"{hash}\tsecondbody line{Environment.NewLine}");
            // As upstream, whose TextBox.Lines ends with the empty line after git's last newline: the HTML keeps it as a <br/>.
            Click(dialog, "buttonCopyAsHtml");
            Copied(dialog).Should().Be(
                "<p>Commit log from 'HEAD~1' to 'HEAD' (most recent changes are listed on top):</p>" +
                $"<table>\r\n<tr>\r\n  <td>{hash}</td>\r\n  <td>secondbody line<br/></td>\r\n</tr>\r\n</table>");
            return true;
        });

        _messages.Should().Equal("'From' commit must be specified");

        static string? Copied(Window dialog) => (string?)dialog.GetType().GetProperty("CopiedText")!.GetValue(dialog);
    }

    [AvaloniaTest]
    public void Release_notes_html_is_upstreams_cf_html_with_the_offsets_of_the_document_and_the_fragment()
    {
        string cfHtml = (string)PluginType("ReleaseNotesGenerator", "HtmlClipboard").GetMethod("CreateCfHtml")!
            .Invoke(null, ["<b>x</b>"])!;

        cfHtml.Should().Be(
            "Version:0.9\r\nStartHTML:00000097\r\nEndHTML:00000173\r\nStartFragment:00000131\r\nEndFragment:00000139\r\n" +
            "<html><body>\r\n<!--StartFragment--><b>x</b><!--EndFragment-->\r\n</body></html>");
        cfHtml[131..139].Should().Be("<b>x</b>");
    }

    [AvaloniaTest]
    public void Gource_starts_the_program_in_the_repository_and_the_plugin_saves_its_path_and_arguments()
    {
        string folder = NewFolder();
        string recorded = Path.Combine(folder, "arguments.txt");
        string gource = FakeGource(folder, recorded);
        IGitPlugin plugin = LoadPlugin("Gource");
        MainWindow window = OpenWindow(plugin);
        SettingsSource settings = TestSettings(plugin);
        settings.SetString("Path to Gource", gource);

        RunPlugin(window, plugin, dialog =>
        {
            Find<TextBox>(dialog, "GourcePathBox").Text.Should().Be(gource);
            Find<TextBox>(dialog, "WorkingDirBox").Text.Should()
                .Be(_repo.Path.ToNativePath() + Path.DirectorySeparatorChar);
            Find<TextBox>(dialog, "ArgumentsBox").Text.Should().Be("--hide filenames --user-image-dir \"$(AVATARS)\"");
            Find<TextBox>(dialog, "ArgumentsBox").Text =
                "--hide filenames --user-image-dir \"$(AVATARS)\" --seconds-per-day 1";
            Click(dialog, "button1");
            return true;
        });

        WaitUntil(() => File.Exists(recorded) && File.ReadAllText(recorded).Contains("--seconds-per-day"));
        File.ReadAllText(recorded).Should().Contain("--hide filenames --user-image-dir")
            .And.Contain(Path.Combine(Path.GetTempPath(), "GitAvatars").TrimEnd(Path.DirectorySeparatorChar));
        settings.GetString("Arguments", null).Should().EndWith("--seconds-per-day 1");
        settings.GetString("Path to Gource", null).Should().Be(gource);
        _messages.Should().BeEmpty();

        // A program that writes its arguments to a file, in the platform's script language.
        static string FakeGource(string folder, string output)
        {
            if (OperatingSystem.IsWindows())
            {
                string script = Path.Combine(folder, "gource.cmd");
                File.WriteAllText(script, $"@echo %* > \"{output}\"\r\n");
                return script;
            }

            string shell = Path.Combine(folder, "gource");
            File.WriteAllText(shell, $"#!/bin/sh\necho \"$@\" > '{output}'\n");
            File.SetUnixFileMode(shell, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            return shell;
        }
    }

    [AvaloniaTest]
    public void Impact_graph_draws_a_band_per_author_and_names_the_author_under_the_pointer()
    {
        CommitAs("Ann", "2026-01-05T12:00:00", "ann one");
        CommitAs("Bob", "2026-01-14T12:00:00", "bob one");
        CommitAs("Ann", "2026-01-21T12:00:00", "ann two");
        IGitPlugin plugin = LoadPlugin("GitImpact");
        MainWindow window = OpenWindow(plugin);
        bool toggled = false;

        RunPlugin(window, plugin, dialog =>
        {
            Control graph = Find<Control>(dialog, "Graph");
            int Weeks() => (int)graph.GetType().GetProperty("WeekCount")!.GetValue(graph)!;

            IReadOnlyList<string> Authors() =>
                (IReadOnlyList<string>)graph.GetType().GetProperty("Authors")!.GetValue(graph)!;

            if (Weeks() < 3 || !Authors().Contains("Ann"))
            {
                return false;
            }

            if (!toggled)
            {
                // Bob is not asserted: upstream's ImpactLoader skips every commit that follows another in git's output (its
                // parse loop steps over the next header), and the new shell uses it unchanged.
                Authors().Should().Contain(["Test", "Ann"]);

                // The first column is the oldest week, which has Ann's commit only.
                bool selected = (bool)graph.GetType().GetMethod("TrySelectAuthorAt")!
                    .Invoke(graph, [new Avalonia.Point(10, 2)])!;
                selected.Should().BeTrue();
                Find<TextBlock>(dialog, "lblAuthor").Text.Should().Be("Ann (2 Commits, 4 Changed Lines)");
                Find<Border>(dialog, "AuthorColor").IsVisible.Should().BeTrue();

                // Upstream's submodules option reloads the graph from the start.
                Find<CheckBox>(dialog, "cbIncludingSubmodules").IsChecked = true;
                Find<TextBlock>(dialog, "lblAuthor").IsVisible.Should().BeFalse();
                toggled = true;
                return false;
            }

            return true;
        });

        void CommitAs(string author, string date, string message)
        {
            File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), message);
            _repo.Run("-c", $"user.name={author}", "commit", "-q", "-am", message, "--date", date);
        }
    }

    [AvaloniaTest]
    public void Statistics_counts_commits_per_contributor_and_lines_of_code_and_test_code()
    {
        Directory.CreateDirectory(Path.Combine(_repo.Path, "src"));
        Directory.CreateDirectory(Path.Combine(_repo.Path, "tests"));
        File.WriteAllText(Path.Combine(_repo.Path, "src", "Program.cs"), "// A comment\n\nclass Program\n{\n}\n");
        File.WriteAllText(Path.Combine(_repo.Path, "tests", "ProgramTests.cs"), "class ProgramTests\n{\n}\n");
        _repo.Run("add", ".");
        _repo.Run("-c", "user.name=Ann", "commit", "-q", "-m", "code");
        IGitPlugin plugin = LoadPlugin("GitStatistics");
        MainWindow window = OpenWindow(plugin);

        RunPlugin(window, plugin, dialog =>
        {
            if (Find<TextBlock>(dialog, "TotalCommits").Text == "Total commits"
                || !(bool)dialog.GetType().GetProperty("IsLinesOfCodeDone")!.GetValue(dialog)!)
            {
                return false;
            }

            Dispatcher.UIThread.RunJobs();
            Find<TextBlock>(dialog, "TotalCommits").Text.Should().Be("3 Commits");
            Find<SelectableTextBlock>(dialog, "CommitStatistics").Text.Should().Contain("2 Test").And.Contain("1 Ann");
            Find<TextBlock>(dialog, "TotalLinesOfCode").Text.Should().MatchRegex(@"^[1-9]\d* Lines of code$");
            Find<SelectableTextBlock>(dialog, "LinesOfCodePerLanguageText").Text.Should()
                .Contain("Lines of code in .cs files");
            Find<TextBlock>(dialog, "TotalLinesOfTestCode").Text.Should().MatchRegex(@"^[1-9]\d* Lines of test code$");
            Find<SelectableTextBlock>(dialog, "LinesOfCodePerTypeText").Text.Should().Contain("Comment lines")
                .And.Contain("Blank lines");

            // A slice per contributor; the pointer over a slice shows its text.
            Control pie = Find<Control>(dialog, "CommitCountPie");
            ((IReadOnlyList<decimal>)pie.GetType().GetProperty("Values")!.GetValue(pie)!).Should().HaveCount(2);
            int slice = (int)pie.GetType().GetMethod("SliceAt")!
                .Invoke(pie, [new Avalonia.Point(pie.Bounds.Width / 2, pie.Bounds.Height / 2 - 20)])!;
            slice.Should().BeInRange(0, 1);
            return true;
        });

        _messages.Should().BeEmpty();
    }

    // Loads the plugin from the Plugins folder next to the app, as upstream's ManagedExtensibility finds it, and makes it the
    // only plugin the app sees.
    private static IGitPlugin LoadPlugin(string name)
    {
        Type type = PluginAssembly(name).GetTypes().Single(candidate =>
            candidate.GetCustomAttributes<ExportAttribute>().Any(export => export.ContractType == typeof(IGitPlugin)));
        IGitPlugin plugin = (IGitPlugin)Activator.CreateInstance(type)!;
        TestAppBuilder.UsePlugins(plugin);
        AppServices.Plugins.Load();
        return plugin;
    }

    // The tests do not reference the plugins: the app loads them from its Plugins folder, and so do the tests, so each plugin
    // assembly is loaded once.
    private static Assembly PluginAssembly(string name)
    {
        string assemblyName = "GitExtensions.Plugins." + name;
        return Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory, "Plugins", assemblyName,
            assemblyName + ".dll"));
    }

    private static Type PluginType(string plugin, string typeName)
        => PluginAssembly(plugin).GetType($"GitExtensions.Plugins.{plugin}.{typeName}", throwOnError: true)!;

    private static string RepositoryRoot([CallerFilePath] string testFile = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testFile)!, "..", "..", ".."));

    // Upstream's GitPluginBase.Register points a plugin at the open repository's settings (which include the user's global
    // settings file), so a test gives it settings in memory once the repository is open.
    private static SettingsSource TestSettings(IGitPlugin plugin)
    {
        ((GitPluginSettingsContainer)plugin.SettingsContainer!).SetSettingsSource(TestAppBuilder.PluginSettings);
        return plugin.SettingsContainer.GetSettingsSource();
    }

    private MainWindow OpenWindow(IGitPlugin plugin)
    {
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        MenuItem menu = Find<MenuItem>(window, "PluginsMenu");
        WaitUntil(() => menu.Items.OfType<MenuItem>().Any(item => item.Tag == plugin));
        Find<TextBox>(window, "PathBox").Text = _repo.Path;
        Click(window, "OpenButton");
        WaitUntil(() => Find<Button>(window, "OpenButton").IsEnabled);
        return window;
    }

    // Runs the plugin from the Plugins menu. Its window is modal and the click returns only when it closes, so the test drives
    // it from a timer: step gets the window on each tick until it returns true, and the window is then closed if the step
    // left it open. A window still open at the timeout is closed, so a failure cannot hang the run.
    private static void RunPlugin(MainWindow window, IGitPlugin plugin, Func<Window, bool> step)
    {
        DateTime deadline = DateTime.UtcNow + _timeout;
        bool timedOut = false;
        Exception? failure = null;
        DispatcherTimer driver = new() { Interval = TimeSpan.FromMilliseconds(50) };
        driver.Tick += (_, _) =>
        {
            if (window.OwnedWindows.FirstOrDefault(owned => owned.IsVisible && owned is not MainWindow) is not
                { } dialog)
            {
                return;
            }

            try
            {
                timedOut = DateTime.UtcNow > deadline;
                if (timedOut || step(dialog))
                {
                    driver.Stop();
                    dialog.Close();
                }
            }
            catch (Exception ex)
            {
                failure = ex;
                driver.Stop();
                dialog.Close();
            }
        };
        driver.Start();

        MenuItem item = Find<MenuItem>(window, "PluginsMenu").Items.OfType<MenuItem>()
            .Single(entry => entry.Tag == plugin);
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        driver.Stop();

        if (failure is not null)
        {
            throw new AssertionException($"The plugin window step failed: {failure}");
        }

        timedOut.Should().BeFalse("the plugin window should finish before the timeout");
    }

    private string NewFolder()
    {
        string folder = Path.Combine(Path.GetTempPath(), "xplat-plugin-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        _folders.Add(folder);
        return folder;
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
}
