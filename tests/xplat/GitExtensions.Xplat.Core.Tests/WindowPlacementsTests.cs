using AwesomeAssertions;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class WindowPlacementsTests
{
    // Written by upstream's XmlSerializer for WindowPositionList (GitUI/WindowPositionList.cs).
    private const string UpstreamFile = """
        <?xml version="1.0" encoding="utf-8"?>
        <WindowPositionList xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
          <WindowPositions>
            <WindowPosition>
              <Rect>
                <Location><X>10</X><Y>20</Y></Location>
                <Size><Width>800</Width><Height>600</Height></Size>
                <X>10</X><Y>20</Y><Width>800</Width><Height>600</Height>
              </Rect>
              <DeviceDpi>144</DeviceDpi>
              <State>Maximized</State>
              <Name>FormBrowse</Name>
            </WindowPosition>
            <WindowPosition>
              <Rect>
                <Location><X>-5</X><Y>7</Y></Location>
                <Size><Width>640</Width><Height>480</Height></Size>
                <X>-5</X><Y>7</Y><Width>640</Width><Height>480</Height>
              </Rect>
              <State>Normal</State>
              <Name>FormCommit</Name>
            </WindowPosition>
          </WindowPositions>
        </WindowPositionList>
        """;

    private string _folder = null!;

    [SetUp]
    public void Setup()
    {
        _folder = Path.Combine(Path.GetTempPath(), "xplat-placements-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_folder, recursive: true);

    [Test]
    public void Load_should_read_the_file_upstream_writes()
    {
        string path = Path.Combine(_folder, "WindowPositions.xml");
        File.WriteAllText(path, UpstreamFile);
        WindowPositionsFileStore store = new(path);

        store.Load("FormBrowse").Should().Be(new WindowPlacement(10, 20, 800, 600, 144, Maximized: true));
        store.Load("FormCommit").Should().Be(new WindowPlacement(-5, 7, 640, 480, 96, Maximized: false));
        store.Load("Xplat.MainWindow").Should().BeNull();
    }

    [Test]
    public void Save_should_keep_the_entries_of_other_windows()
    {
        string path = Path.Combine(_folder, "WindowPositions.xml");
        File.WriteAllText(path, UpstreamFile);
        WindowPositionsFileStore store = new(path);

        store.Save("Xplat.MainWindow", new WindowPlacement(1, 2, 300, 200, 120, Maximized: false));
        store.Save("Xplat.MainWindow", new WindowPlacement(3, 4, 500, 400, 96, Maximized: true));

        store.Load("FormBrowse").Should().Be(new WindowPlacement(10, 20, 800, 600, 144, Maximized: true));
        store.Load("Xplat.MainWindow").Should().Be(new WindowPlacement(3, 4, 500, 400, 96, Maximized: true));
        File.ReadAllText(path).Split("<Name>Xplat.MainWindow</Name>").Should().HaveCount(2, "the entry is replaced, not repeated");
    }

    [Test]
    public void Save_should_write_the_element_order_upstream_writes()
    {
        string path = Path.Combine(_folder, "sub", "WindowPositions.xml");
        WindowPositionsFileStore store = new(path);

        store.Save("Xplat.DiffWindow", new WindowPlacement(10, 20, 800, 600, 96, Maximized: false));

        string written = File.ReadAllText(path);
        written.Should().Contain("<WindowPositionList xmlns:xsi=");
        written.IndexOf("<Location>", StringComparison.Ordinal).Should().BeLessThan(written.IndexOf("<Size>", StringComparison.Ordinal));
        written.IndexOf("<State>Normal</State>", StringComparison.Ordinal)
            .Should().BeLessThan(written.IndexOf("<Name>Xplat.DiffWindow</Name>", StringComparison.Ordinal));
        written.Should().NotContain("<DeviceDpi>", "96 is the default upstream leaves out");
    }

    [Test]
    public void A_damaged_file_should_give_no_placement_and_be_replaced_on_save()
    {
        string path = Path.Combine(_folder, "WindowPositions.xml");
        File.WriteAllText(path, "<WindowPositionList><WindowPositions><WindowPosition><Rect>");
        WindowPositionsFileStore store = new(path);

        store.Load("FormBrowse").Should().BeNull();
        store.Save("Xplat.MainWindow", new WindowPlacement(1, 2, 300, 200, 96, Maximized: false));

        store.Load("Xplat.MainWindow").Should().Be(new WindowPlacement(1, 2, 300, 200, 96, Maximized: false));
    }
}
