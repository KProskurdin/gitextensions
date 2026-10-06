using System.Globalization;
using System.Xml.Linq;
using GitCommands;

namespace GitExtensions.Xplat.Core.Settings;

/// <summary>
///  Where a window was and how big it was, in physical pixels at <see cref="DeviceDpi"/>, as upstream's
///  <c>WindowPosition</c> records it.
/// </summary>
public sealed record WindowPlacement(int X, int Y, int Width, int Height, int DeviceDpi, bool Maximized);

/// <summary>
///  Remembers window placements by window name.
/// </summary>
public interface IWindowPlacementStore
{
    WindowPlacement? Load(string name);

    void Save(string name, WindowPlacement placement);
}

/// <summary>
///  Keeps placements in upstream's <c>WindowPositions.xml</c> (in the local application data folder), in the format upstream's
///  <c>XmlSerializer</c> writes. Upstream's <c>WindowPositionList</c> is in WinForms-only <c>GitUI</c>, so the file is read
///  and written here; entries of other windows, including the WinForms app's, are kept as they are. The new shell's windows
///  use their own names (e.g. "Xplat.MainWindow"), so the two apps never move each other's windows.
/// </summary>
public sealed class WindowPositionsFileStore : IWindowPlacementStore
{
    private const string FileName = "WindowPositions.xml";
    private const int DefaultDpi = 96;
    private static readonly XNamespace _xsi = "http://www.w3.org/2001/XMLSchema-instance";
    private static readonly XNamespace _xsd = "http://www.w3.org/2001/XMLSchema";

    private readonly string _path;

    public WindowPositionsFileStore(string path)
    {
        _path = path;
    }

    /// <summary>
    ///  The store at upstream's location.
    /// </summary>
    public static WindowPositionsFileStore Default()
        => new(Path.Join(AppSettings.LocalApplicationDataPath.Value!, FileName));

    public WindowPlacement? Load(string name)
    {
        try
        {
            XElement? position = FindPosition(LoadDocument(), name);
            XElement? rect = position?.Element("Rect");
            if (position is null || rect is null)
            {
                return null;
            }

            int width = ReadInt(rect, "Width");
            int height = ReadInt(rect, "Height");
            if (width <= 0 || height <= 0)
            {
                return null;
            }

            int dpi = position.Element("DeviceDpi") is { } dpiElement ? int.Parse(dpiElement.Value, CultureInfo.InvariantCulture) : DefaultDpi;
            bool maximized = string.Equals(position.Element("State")?.Value, "Maximized", StringComparison.Ordinal);
            return new WindowPlacement(ReadInt(rect, "X"), ReadInt(rect, "Y"), width, height, dpi, maximized);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException or FormatException or OverflowException)
        {
            // A missing or damaged file only loses the placement; the window opens at its default place.
            return null;
        }
    }

    public void Save(string name, WindowPlacement placement)
    {
        try
        {
            // Like upstream, a file that cannot be read is replaced.
            XDocument document;
            try
            {
                document = LoadDocument();
            }
            catch (System.Xml.XmlException)
            {
                document = NewDocument();
            }

            XElement root = document.Root!;
            XElement list = root.Element("WindowPositions") ?? AddChild(root, new XElement("WindowPositions"));
            FindPosition(document, name)?.Remove();
            list.Add(ToElement(name, placement));

            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            document.Save(_path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException)
        {
            // Not saving a placement is not worth an error dialog.
        }
    }

    private XDocument LoadDocument()
    {
        if (File.Exists(_path))
        {
            using FileStream stream = File.Open(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            XDocument existing = XDocument.Load(stream);
            if (existing.Root?.Name == "WindowPositionList")
            {
                return existing;
            }
        }

        return NewDocument();
    }

    private static XDocument NewDocument()
    {
        return new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("WindowPositionList",
                new XAttribute(XNamespace.Xmlns + "xsi", _xsi),
                new XAttribute(XNamespace.Xmlns + "xsd", _xsd),
                new XElement("WindowPositions")));
    }

    private static XElement? FindPosition(XDocument document, string name)
        => document.Root?.Element("WindowPositions")?.Elements("WindowPosition")
            .FirstOrDefault(position => position.Element("Name")?.Value == name);

    private static int ReadInt(XElement parent, string name)
        => int.Parse(parent.Element(name)?.Value ?? throw new FormatException($"{name} is missing"), CultureInfo.InvariantCulture);

    private static XElement AddChild(XElement parent, XElement child)
    {
        parent.Add(child);
        return child;
    }

    // Same element order as upstream's serializer: Rect (Location, Size, X, Y, Width, Height), DeviceDpi unless 96, State, Name.
    private static XElement ToElement(string name, WindowPlacement placement)
    {
        XElement position = new("WindowPosition",
            new XElement("Rect",
                new XElement("Location", new XElement("X", placement.X), new XElement("Y", placement.Y)),
                new XElement("Size", new XElement("Width", placement.Width), new XElement("Height", placement.Height)),
                new XElement("X", placement.X),
                new XElement("Y", placement.Y),
                new XElement("Width", placement.Width),
                new XElement("Height", placement.Height)));
        if (placement.DeviceDpi != DefaultDpi)
        {
            position.Add(new XElement("DeviceDpi", placement.DeviceDpi));
        }

        position.Add(new XElement("State", placement.Maximized ? "Maximized" : "Normal"));
        position.Add(new XElement("Name", name));
        return position;
    }
}

/// <summary>
///  Keeps placements for the life of the process. Tests use it so they never write the user's file.
/// </summary>
public sealed class InMemoryWindowPlacementStore : IWindowPlacementStore
{
    private readonly Dictionary<string, WindowPlacement> _placements = [];

    public WindowPlacement? Load(string name) => _placements.GetValueOrDefault(name);

    public void Save(string name, WindowPlacement placement) => _placements[name] = placement;
}
