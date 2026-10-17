using AwesomeAssertions;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class SplitterLayoutTests
{
    [Test]
    public void The_keys_are_upstreams()
    {
        string source = UpstreamSource("src/app/GitUI/SplitterManager.cs");
        foreach (string suffix in new[] { "_Size", "_Dpi", "_Distance", "_Panel1Collapsed", "_Panel2Collapsed" })
        {
            source.Should().Contain($"_settingName + \"{suffix}\"");
        }

        UpstreamSource("src/app/GitUI/CommandsDialogs/FormBrowse.cs").Should()
            .Contain("new AppSettingsPath(\"FormBrowse\")")
            .And.Contain("AddSplitter(MainSplitContainer, nameof(MainSplitContainer))")
            .And.Contain("AddSplitter(RightSplitContainer, nameof(RightSplitContainer))");
        UpstreamSource("src/app/GitUI/CommandsDialogs/FormCommit.cs").Should()
            .Contain("SplitterManager _splitterManager = new(new AppSettingsPath(\"CommitDialog\"))")
            .And.Contain("AddSplitter(splitMain, nameof(splitMain))");
    }

    [Test]
    public void The_stored_distance_is_kept_for_the_same_size_and_dpi()
    {
        SplitterSettings stored = new(800, 96, 300, null, null);

        Restore(stored, 800, 96, SplitterFixedPanel.None).Should().Be(300);
        Restore(stored, 800, 96, SplitterFixedPanel.Panel2).Should().Be(306, "upstream adds the splitter's width");
    }

    [Test]
    public void Another_size_or_dpi_keeps_the_proportion_or_the_fixed_panels_size()
    {
        SplitterSettings stored = new(800, 96, 300, null, null);

        Restore(stored, 1000, 96, SplitterFixedPanel.None).Should().Be(375);
        Restore(stored, 1000, 144, SplitterFixedPanel.Panel1).Should().Be(450, "panel 1 is scaled to the DPI");
        Restore(stored, 1000, 96, SplitterFixedPanel.Panel2).Should().Be(504, "panel 2 keeps its 500 pixels");
    }

    [Test]
    public void A_distance_that_squeezes_a_panel_falls_back_to_the_default_or_is_not_restored()
    {
        SplitterSettings stored = new(800, 96, 790, null, null);

        Restore(stored, 800, 96, SplitterFixedPanel.None).Should().BeNull();
        Restore(stored, 800, 96, SplitterFixedPanel.None, defaultDistance: 200).Should().Be(200);
        Restore(SplitterSettings.None, 800, 96, SplitterFixedPanel.None, defaultDistance: 200).Should().Be(200);
        Restore(SplitterSettings.None, 800, 96, SplitterFixedPanel.None).Should().BeNull();
    }

    private static int? Restore(SplitterSettings stored, int size, int dpi, SplitterFixedPanel fixedPanel,
        int? defaultDistance = null)
        => SplitterLayout.RestoredDistance(stored, size, dpi, fixedPanel, splitterWidth: 6, panel1MinSize: 25,
            panel2MinSize: 25, defaultDistance);

    private static string UpstreamSource(string relativePath,
        [System.Runtime.CompilerServices.CallerFilePath] string here = "")
        => File.ReadAllText(
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "..", relativePath)));
}
