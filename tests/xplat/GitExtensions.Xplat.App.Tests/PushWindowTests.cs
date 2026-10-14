using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using GitCommands.Git;
using GitCommands.Remotes;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class PushWindowTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    [AvaloniaTest]
    public void The_window_shows_upstreams_tabs_and_proposes_the_branch()
    {
        PushWindow push = NewWindow();
        push.Show();

        push.Title.Should().Be("Push (C:/repo/)");
        Find<TabControl>(push, "PushTabs").Items.OfType<TabItem>().Select(tab => tab.Header).Should()
            .Equal("Push branches", "Push tags", "Push multiple branches");
        Find<ComboBox>(push, "RemotesBox").SelectedItem.Should().Be("origin");
        Find<ComboBox>(push, "BranchBox").SelectedItem.Should().Be("main");
        Find<ComboBox>(push, "RemoteBranchBox").Text.Should().Be("main");
        Find<ComboBox>(push, "DestinationBox").IsEnabled.Should().BeFalse();
        Find<Control>(push, "OptionsPanel").IsVisible.Should().BeFalse();

        Click(push, "ShowOptionsLink");
        Find<RadioButton>(push, "PushToUrlRadio").IsChecked = true;

        Find<Control>(push, "OptionsPanel").IsVisible.Should().BeTrue();
        Find<ComboBox>(push, "DestinationBox").IsEnabled.Should().BeTrue();
        Find<ComboBox>(push, "DestinationBox").Text.Should().Be("https://example.com/origin.git");
        push.Close();
    }

    [AvaloniaTest]
    public void Push_of_a_tag_closes_with_upstreams_request()
    {
        Window owner = new();
        owner.Show();
        PushWindow push = NewWindow();
        Task<PushRequest?> result = push.ShowDialog<PushRequest?>(owner);
        Find<TabControl>(push, "PushTabs").SelectedIndex = 1;
        Find<ComboBox>(push, "TagBox").SelectedItem = "v1";
        Find<CheckBox>(push, "ForcePushTagsCheck").IsChecked = true;

        Click(push, "PushButton");

        WaitUntil(() => result.IsCompleted);
        result.Result.Should().Be(new PushRequest("origin", "", "", ForcePushOptions.Force, false, PushKind.Tag, "v1"));
        owner.Close();
    }

    [AvaloniaTest]
    public void A_new_branch_is_confirmed_and_a_force_push_is_offered_as_force_with_lease()
    {
        Window owner = new();
        owner.Show();
        PushWindow push = NewWindow();
        Task<PushRequest?> result = push.ShowDialog<PushRequest?>(owner);
        Click(push, "ShowOptionsLink");
        Find<CheckBox>(push, "ForcePushCheck").IsChecked = true;

        Click(push, "PushButton");

        WaitUntil(() => push.OwnedWindows.OfType<ConfirmWindow>().Any());
        Click(push.OwnedWindows.OfType<ConfirmWindow>().Single(), "ConfirmButton");
        WaitUntil(() => push.OwnedWindows.OfType<MessageBoxWindow>().Any());
        MessageBoxWindow tracking = push.OwnedWindows.OfType<MessageBoxWindow>().Single();
        Find<SelectableTextBlock>(tracking, "MessageText").Text.Should().Be(
            "The branch main does not have a tracking reference. Do you want to add a tracking reference to main?");
        Click(tracking, "NoButton");
        WaitUntil(() => push.OwnedWindows.OfType<MessageBoxWindow>().Any(box => box != tracking));
        MessageBoxWindow question = push.OwnedWindows.OfType<MessageBoxWindow>().Single(box => box != tracking);
        Find<SelectableTextBlock>(question, "MessageText").Text.Should().Be(PushDialog.UseForceWithLeaseInstead);
        Click(question, "YesButton");
        WaitUntil(() => result.IsCompleted);
        result.Result.Should().Be(new PushRequest("origin", "main", "main", ForcePushOptions.ForceWithLease, false,
            RecursiveSubmodules: 1));
        owner.Close();
    }

    private static PushWindow NewWindow()
        => new(new PushDialog(new PushRepository("main",
                [new ConfigFileRemote { Name = "origin", Url = "https://example.com/origin.git" }],
                [new LocalBranch("main")], [], ["v1"], "origin", null, IsBare: false, AutoSetupMerge: null,
                (_, _) => null)),
            TestAppBuilder.Preferences, "C:/repo/");

    // A local branch without a module behind it.
    private sealed class LocalBranch(string name) : IGitRef
    {
        public string Name => name;

        public ObjectId ObjectId => ObjectId.WorkTreeId;

        public string? Guid => null;

        public string CompleteName => $"refs/heads/{name}";

        public string LocalName => name;

        public string MergeWith { get; set; } = "";

        public string Remote => "";

        public string TrackingRemote { get; set; } = "";

        public bool IsHead => true;

        public bool IsRemote => false;

        public bool IsTag => false;

        public bool IsDereference => false;

        public bool IsStash => false;

        public bool IsSelected { get; set; }

        public bool IsSelectedHeadMergeSource { get; set; }

        public bool IsBisect => false;

        public bool IsBisectGood => false;

        public bool IsBisectBad => false;

        public IGitModule Module => throw new NotSupportedException();

        public bool IsTrackingRemote(IGitRef? remote) => false;
    }

    private static void Click(Window window, string name)
        => Find<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static T Find<T>(Window window, string name) where T : Control
        => window.FindControl<T>(name) ??
           window.GetLogicalDescendants().OfType<T>().FirstOrDefault(control => control.Name == name) ??
           throw new InvalidOperationException($"Control {name} not found");

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