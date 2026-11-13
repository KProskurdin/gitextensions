using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using GitCommands;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

/// <summary>
///  A WinForms toolstrip button is announced by its <c>Text</c>, also when it shows only its image; the new shell's tool
///  buttons give screen readers the same name, from their text or else their tooltip.
/// </summary>
internal sealed class ToolButtonNamesTests
{
    // Upstream's plain image buttons that clear a filter (FileStatusList's DeleteFilterButton and the git-grep box's): no
    // text and no tooltip, so upstream does not name them either.
    private static readonly HashSet<string> _unnamedUpstream =
        ["DeleteFilterButton", "DiffFilesGitGrepDeleteButton", "FileTreeGitGrepDeleteButton"];

    [AvaloniaTest]
    public void Every_tool_button_of_the_browse_window_has_an_accessible_name()
    {
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            List<Control> tools = [.. window.GetLogicalDescendants().OfType<Control>().Where(IsToolButton)];
            tools.Should().NotBeEmpty();

            List<string> unnamed =
            [
                .. tools
                    .Where(button => !_unnamedUpstream.Contains(button.Name ?? ""))
                    .Where(button => !IsMeaningful(ControlAutomationPeer.CreatePeerForElement(button).GetName()))
                    .Select(button =>
                        $"{button.Name ?? "?"} ({ControlAutomationPeer.CreatePeerForElement(button).GetName()})")
            ];

            string.Join(", ", unnamed).Should().BeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTest]
    public void An_image_only_tool_button_is_named_by_its_tooltip_and_a_text_button_by_its_text()
    {
        ToolButton imageOnly = new() { Image = "Pull" };
        ToolTip.SetTip(imageOnly, "Pull - fetch and merge");
        ToolButton withText = new() { Image = "Push", Text = "_Push" };
        ToolToggleButton toggle = new() { Image = "Pull" };
        ToolTip.SetTip(toggle, "Toggle");
        Window window = new() { Content = new StackPanel { Children = { imageOnly, withText, toggle } } };
        window.Show();
        try
        {
            ControlAutomationPeer.CreatePeerForElement(imageOnly).GetName().Should().Be("Pull - fetch and merge");
            ControlAutomationPeer.CreatePeerForElement(withText).GetName().Should().Be("Push");
            ControlAutomationPeer.CreatePeerForElement(toggle).GetName().Should().Be("Toggle");
        }
        finally
        {
            window.Close();
        }
    }

    private static bool IsToolButton(Control control)
        => control is ToolButton or ToolToggleButton or ToolDropDownButton or ToolSplitButton;

    // A name a reader can say: not empty and not a type name such as "Avalonia.Controls.StackPanel".
    private static bool IsMeaningful(string? name)
        => !string.IsNullOrWhiteSpace(name) && !name.StartsWith("Avalonia.", StringComparison.Ordinal);
}
