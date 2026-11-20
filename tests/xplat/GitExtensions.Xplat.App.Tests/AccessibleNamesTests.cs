using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using GitCommands;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

/// <summary>
///  Upstream's WinForms rows and tree nodes are read by their text; the shell's list and tree rows by what they show
///  (<see cref="AccessibleNames"/>), not by their data's <c>ToString</c>.
/// </summary>
internal sealed class AccessibleNamesTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    [Test]
    public void Of_should_name_a_row_by_what_it_shows()
    {
        AccessibleNames.Of(new FileChange("src/a.cs", ChangeKind.Modified, Staged: false)).Should().Be("src/a.cs");
        AccessibleNames.Of("plain").Should().Be("plain");
        AccessibleNames.Of(new { Display = "shown", Other = 1 }).Should().Be("shown", "another row is read by its text");
        AccessibleNames.Of(new { Other = 1 }).Should().BeNull("Avalonia's own name is kept");
    }

    [AvaloniaTest]
    public void Every_row_of_the_browse_windows_grid_and_trees_has_a_name_a_reader_can_say()
    {
        using TestRepository repo = new();
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        window.OpenRepository(repo.Path);
        WaitUntil(() => !window.IsBusy && window.GetVisualDescendants().OfType<ListBoxItem>().Any()
                                        && window.GetVisualDescendants().OfType<TreeViewItem>().Any());

        List<string?> names =
        [
            .. window.GetVisualDescendants().OfType<Control>().Where(item => item is ListBoxItem or TreeViewItem)
                .Select(item => ControlAutomationPeer.CreatePeerForElement(item).GetName())
        ];

        names.Should().Contain(name => name != null && name.StartsWith("second, Test, ", StringComparison.Ordinal),
            "a commit is read by its subject, author and date");
        names.Should().OnlyContain(name => !string.IsNullOrWhiteSpace(name) && !name.Contains(" { ", StringComparison.Ordinal)
                                           && !name.StartsWith("Avalonia.", StringComparison.Ordinal));
        window.Close();
    }

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
            Thread.Sleep(10);
        }
    }
}
