using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using GitCommands;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

/// <summary>
///  The revision grid's context menu against upstream's (<c>RevisionGridControl.Designer</c>'s mainContextMenu) and its
///  submenus for a commit (upstream's <c>ContextMenuOpening</c>).
/// </summary>
internal sealed partial class GridMenuTests
{
    private const string GridDesigner = "src/app/GitUI/UserControls/RevisionGrid/RevisionGridControl.Designer.cs";
    private const string GridControl = "src/app/GitUI/UserControls/RevisionGrid/RevisionGridControl.cs";
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    // Upstream sets this item's text in code (CopyContextMenuItem); "Browse files at this commit..." is the new shell's own.
    private static readonly string[] _notInDesigner = ["_Copy to clipboard", "Browse files at this commit..."];

    private TestRepository _repo = null!;

    [SetUp]
    public void SetUp()
    {
        _repo = new TestRepository();
        _repo.Run("branch", "topic", "HEAD~1");
        _repo.Run("tag", "v1", "HEAD~1");
    }

    [TearDown]
    public void TearDown()
    {
        TestAppBuilder.Preferences.RevisionGraphShowArtificialCommits = false;
        _repo.Dispose();
    }

    [AvaloniaTest]
    public void The_menu_has_upstreams_items_in_upstreams_order()
    {
        Dictionary<string, string> texts = UpstreamTexts();
        List<string> upstream =
            [.. UpstreamMenuItems().Select(item => item.Name).Where(texts.ContainsKey).Select(item => texts[item])];
        MainWindow window = OpenWindow();

        List<string> ours =
        [
            .. window.FindControl<ContextMenu>("CommitContextMenu")!.Items.OfType<MenuItem>()
                .Select(item => item.Header as string ?? "").Where(header => !_notInDesigner.Contains(header))
        ];

        ours.Should().Equal(upstream.Where(ours.Contains), "the items come in upstream's order at run time");
        ours.Except(upstream).Should().BeEmpty("every item has upstream's text and access key");
        window.Close();
    }

    [AvaloniaTest]
    public void The_submenus_list_the_commits_branches_and_tags()
    {
        MainWindow window = OpenWindow();
        SelectCommit(window, "first");

        OpenMenu(window);

        Headers(window, "GridCheckoutBranchMenuItem").Should().Equal("topic");
        Headers(window, "GridRenameBranchMenuItem").Should().Equal("topic");
        Headers(window, "GridDeleteBranchMenuItem").Should().Equal("topic");
        Headers(window, "GridDeleteTagMenuItem").Should().Equal("v1");
        Headers(window, "GridMergeBranchMenuItem").Should().Equal("v1", "topic");
        Find<MenuItem>(window, "GridResetChangesMenuItem").IsVisible.Should().BeFalse("it is for the artificial commits");
        Find<MenuItem>(window, "CopyToClipboardMenuItem").Items.OfType<MenuItem>().Select(item => item.Header as string)
            .Should().Contain(["Branches", "_1:   topic", "Tags", "_2:   v1"]);
        window.Close();
    }

    [AvaloniaTest]
    public void The_artificial_commits_get_reset_changes_and_commit_only()
    {
        TestAppBuilder.Preferences.RevisionGraphShowArtificialCommits = true;
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "changed");
        MainWindow window = OpenWindow();
        ListBox commits = Find<ListBox>(window, "CommitList");
        WaitUntil(() => commits.Items.Cast<CommitListItem>().Any(item => ArtificialCommits.IsArtificial(item.Row.Hash)));
        commits.SelectedItem = commits.Items.Cast<CommitListItem>().First(item => ArtificialCommits.IsArtificial(item.Row.Hash));
        WaitUntil(() => window.FindControl<ContextMenu>("CommitContextMenu") is not null);

        OpenMenu(window);

        Find<MenuItem>(window, "GridResetChangesMenuItem").IsVisible.Should().BeTrue();
        Find<MenuItem>(window, "GridCommitMenuItem").IsVisible.Should().BeTrue();
        Find<MenuItem>(window, "CheckoutCommitMenuItem").IsVisible.Should().BeFalse();
        Find<MenuItem>(window, "CopyToClipboardMenuItem").IsVisible.Should().BeFalse();
        window.Close();
    }

    [AvaloniaTest]
    public void Delete_tag_opens_upstreams_window_on_the_tag_and_select_in_left_panel_selects_it()
    {
        MainWindow window = OpenWindow();
        SelectCommit(window, "first");
        OpenMenu(window);

        Find<MenuItem>(window, "GridDeleteTagMenuItem").Items.OfType<MenuItem>().Single()
            .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        WaitUntil(() => window.OwnedWindows.OfType<DeleteTagWindow>().Any());
        DeleteTagWindow delete = window.OwnedWindows.OfType<DeleteTagWindow>().Single();
        delete.FindControl<ComboBox>("Tags")!.Text.Should().Be("v1");
        delete.Close();

        Find<MenuItem>(window, "GridSelectInLeftPanelMenuItem").Items.OfType<MenuItem>()
            .Single(item => (item.Header as TextBlock)?.Text == "topic")
            .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        WaitUntil(() => (Find<TreeView>(window, "BranchTreeView").SelectedItem as RepoObjectNode)?.Branch?.Name == "topic");
        window.Close();
    }

    [AvaloniaTest]
    public void The_view_submenu_has_the_view_menus_commands_and_runs_them()
    {
        MainWindow window = OpenWindow();
        SelectCommit(window, "first");
        MenuItem showTags = Find<MenuItem>(window, "ShowTagsMenuItem");
        bool shown = showTags.IsChecked;
        OpenMenu(window);

        MenuItem view = Find<MenuItem>(window, "GridViewMenuItem");
        view.Items.Select(entry => (entry as MenuItem)?.Header).Should()
            .Equal(Find<MenuItem>(window, "ViewMenu").Items.Select(entry => (entry as MenuItem)?.Header));
        MenuItem copy = view.Items.OfType<MenuItem>().Single(item => item.Header as string == "Show _tags");
        copy.IsChecked.Should().Be(shown);
        copy.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        showTags.IsChecked.Should().Be(!shown, "the copy runs the View menu's command");

        OpenMenu(window);
        view.Items.OfType<MenuItem>().Single(item => item.Header as string == "Show _tags").IsChecked.Should().Be(!shown);
        showTags.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        showTags.IsChecked.Should().Be(shown);
        window.Close();
    }

    [AvaloniaTest]
    public void A_right_clicked_label_gets_upstreams_focused_menu_for_that_ref()
    {
        Dictionary<string, string> texts = UpstreamTexts();
        List<string> upstreamAdvanced =
        [
            .. UpstreamMenuItems().Where(item => item.Advanced).Select(item => item.Name).Where(texts.ContainsKey)
                .Select(item => texts[item])
        ];
        MainWindow window = OpenWindow();
        SelectCommit(window, "first");

        RightClickLabel(window, "topic", RawInputModifiers.None);

        MenuItem otherActions = Find<MenuItem>(window, "OtherActionsMenuItem");
        otherActions.IsVisible.Should().BeTrue();
        List<string> moved = [.. otherActions.Items.OfType<MenuItem>().Select(item => item.Header as string ?? "")];
        moved.Should().Contain("Checkout _this commit...");
        moved.Where(header => !_notInDesigner.Contains(header)).Should()
            .Equal(upstreamAdvanced.Where(moved.Contains), "upstream's advanced items move, in their order");
        Headers(window, "GridRenameBranchMenuItem").Should().Equal("topic");
        Headers(window, "GridMergeBranchMenuItem").Should().Equal("topic");
        Find<MenuItem>(window, "GridDeleteTagMenuItem").IsVisible.Should().BeFalse("only the clicked ref is offered");
        Find<MenuItem>(window, "CopyToClipboardMenuItem").Items.OfType<MenuItem>().Select(item => item.Header as string)
            .Should().Contain("_1:   topic").And.NotContain("Tags");

        RightClickLabel(window, "topic", RawInputModifiers.Shift);

        otherActions.IsVisible.Should().BeFalse("Shift asks for the whole menu");
        Find<ContextMenu>(window, "CommitContextMenu").Items.Should().Contain(Find<MenuItem>(window, "CheckoutCommitMenuItem"));
        Headers(window, "GridRenameBranchMenuItem").Should().Equal("topic");

        OpenMenu(window);

        otherActions.IsVisible.Should().BeFalse("no label was clicked");
        Headers(window, "GridDeleteTagMenuItem").Should().Equal("v1");
        window.Close();
    }

    // A right click on the commit's label, which opens the menu as upstream's OnGridViewCellMouseDown and
    // ContextMenuOpening see it (raised by hand when the headless platform does not open it on release).
    private static void RightClickLabel(MainWindow window, string text, RawInputModifiers modifiers)
    {
        Dispatcher.UIThread.RunJobs();
        ListBox commits = Find<ListBox>(window, "CommitList");
        RefLabelView label = commits.GetVisualDescendants().OfType<RefLabelView>().First(view => view.Label?.Text == text);
        Point point = label.TranslatePoint(new Point(label.Bounds.Width / 2, label.Bounds.Height / 2), window)!.Value;
        bool opened = false;
        commits.ContextMenu!.Opening += OnOpening;
        window.MouseDown(point, MouseButton.Right, modifiers);
        window.MouseUp(point, MouseButton.Right, modifiers);
        Dispatcher.UIThread.RunJobs();
        if (!opened)
        {
            commits.RaiseEvent(new ContextRequestedEventArgs());
            Dispatcher.UIThread.RunJobs();
        }

        commits.ContextMenu.Opening -= OnOpening;
        commits.ContextMenu.Close();

        void OnOpening(object? sender, System.ComponentModel.CancelEventArgs e) => opened = true;
    }

    private MainWindow OpenWindow()
    {
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        window.OpenRepository(_repo.Path);
        WaitUntil(() => !window.IsBusy);
        return window;
    }

    private static void SelectCommit(MainWindow window, string subject)
    {
        ListBox commits = Find<ListBox>(window, "CommitList");
        commits.SelectedItem = commits.Items.Cast<CommitListItem>().Single(item => item.Row.Subject == subject);
    }

    // The menu fills itself for the selected commit when it opens, as upstream's ContextMenuOpening.
    private static void OpenMenu(Window window)
    {
        ListBox commits = Find<ListBox>(window, "CommitList");
        commits.RaiseEvent(new Avalonia.Input.ContextRequestedEventArgs());
        Dispatcher.UIThread.RunJobs();
        commits.ContextMenu!.Close();
    }

    private static IReadOnlyList<string?> Headers(Window window, string name)
        => [.. Find<MenuItem>(window, name).Items.OfType<MenuItem>().Select(item => (item.Header as TextBlock)?.Text)];

    private static string UpstreamPath(string relativePath, [System.Runtime.CompilerServices.CallerFilePath] string here = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "..", relativePath));

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

    [GeneratedRegex(@"^\s*(?<item>\w+)\.Text = ""(?<text>[^""]*)"";", RegexOptions.Multiline)]
    private static partial Regex TextAssignment();

    // Upstream's _contextMenuItems (RevisionGridControl's constructor): the menu's order at run time, and whether an item
    // goes under "Other actions" in a focused menu.
    private static IReadOnlyList<(string Name, bool Advanced)> UpstreamMenuItems()
    {
        string list = ContextMenuItems().Match(File.ReadAllText(UpstreamPath(GridControl))).Value;
        return [.. ContextMenuItem().Matches(list).Select(match => (match.Groups["item"].Value, match.Groups["kind"].Value == "advanced"))];
    }

    private static Dictionary<string, string> UpstreamTexts()
    {
        Dictionary<string, string> texts = [];
        foreach (Match match in TextAssignment().Matches(File.ReadAllText(UpstreamPath(GridDesigner))))
        {
            texts.TryAdd(match.Groups["item"].Value, match.Groups["text"].Value.Replace("&", "_", StringComparison.Ordinal));
        }

        return texts;
    }

    [GeneratedRegex(@"_contextMenuItems = \[[^\]]*\]")]
    private static partial Regex ContextMenuItems();

    [GeneratedRegex(@"\((?<item>\w+), (?<kind>top|advanced)\)")]
    private static partial Regex ContextMenuItem();
}
