using System.Globalization;
using System.Xml.Linq;
using Keys = System.Windows.Forms.Keys;

namespace GitExtensions.Xplat.Core.Settings;

/// <summary>
///  One hotkey as upstream stores it: the command's code and name in its form's command enum, and the key as a WinForms
///  <see cref="Keys"/> value (key code plus modifier bits).
/// </summary>
public sealed record UpstreamHotkey(int CommandCode, string Name, Keys KeyData);

/// <summary>
///  Reads and writes upstream's <c>SerializedHotkeys</c> setting: the <c>XmlSerializer</c> output of
///  <c>HotkeySettings[]</c> (upstream <c>GitUI/Hotkey</c>, WinForms-only), one <c>HotkeySettings</c> per form. Only the
///  given form's entries are changed; every other form's hotkeys are written back as they were.
/// </summary>
/// <remarks>
///  <see cref="Keys"/> is not a pure flags enum, so the serializer writes every name whose bits are set (Ctrl+, comes out as
///  a long list). OR-ing the names gives the value back, which is how upstream reads it; this writes the short form (e.g.
///  "Space Control"), which upstream reads the same way.
/// </remarks>
public static class UpstreamHotkeys
{
    /// <summary>
    ///  Upstream <c>FormBrowse.HotkeySettingsName</c>: the section of the browse window's hotkeys.
    /// </summary>
    public const string BrowseFormName = "Browse";

    /// <summary>
    ///  Upstream <c>FormCommit.HotkeySettingsName</c>: the section of the commit window's hotkeys.
    /// </summary>
    public const string CommitFormName = "Commit";

    /// <summary>
    ///  Upstream <c>RevisionGridControl.HotkeySettingsName</c>.
    /// </summary>
    public const string RevisionGridName = "RevisionGrid";

    /// <summary>
    ///  Upstream <c>FileViewer.HotkeySettingsName</c>.
    /// </summary>
    public const string FileViewerName = "FileViewer";

    /// <summary>
    ///  Upstream <c>RepoObjectsTree.HotkeySettingsName</c>.
    /// </summary>
    public const string LeftPanelName = "LeftPanel";

    /// <summary>
    ///  Upstream <c>FormResolveConflicts.HotkeySettingsName</c>.
    /// </summary>
    public const string ResolveConflictsName = "FormMergeConflicts";

    /// <summary>
    ///  Upstream <c>FormSettings.HotkeySettingsName</c>: the section of the user scripts' hotkeys, one command per script
    ///  with the script's <c>HotkeyCommandIdentifier</c> as its code.
    /// </summary>
    public const string ScriptsName = "Scripts";

    /// <summary>
    ///  The hotkeys stored for <paramref name="formName"/>, by command name; empty when none are stored.
    /// </summary>
    public static IReadOnlyDictionary<string, UpstreamHotkey> Read(string? serialized, string formName)
    {
        Dictionary<string, UpstreamHotkey> hotkeys = new(StringComparer.Ordinal);
        foreach (UpstreamHotkey hotkey in ReadAll(serialized, formName))
        {
            hotkeys[hotkey.Name] = hotkey;
        }

        return hotkeys;
    }

    /// <summary>
    ///  The hotkeys stored for <paramref name="formName"/>, by command code, which is how upstream matches them
    ///  (<c>HotkeySettingsManager.MergeIntoDefaultSettings</c>); empty when none are stored.
    /// </summary>
    public static IReadOnlyDictionary<int, UpstreamHotkey> ReadByCode(string? serialized, string formName)
    {
        Dictionary<int, UpstreamHotkey> hotkeys = [];
        foreach (UpstreamHotkey hotkey in ReadAll(serialized, formName))
        {
            hotkeys[hotkey.CommandCode] = hotkey;
        }

        return hotkeys;
    }

    /// <summary>
    ///  <paramref name="serialized"/> with <paramref name="hotkeys"/> set for <paramref name="formName"/>, adding the form and
    ///  commands that are missing.
    /// </summary>
    public static string Write(string? serialized, string formName, IEnumerable<UpstreamHotkey> hotkeys)
    {
        XDocument document = Parse(serialized) ?? NewDocument();
        XElement root = document.Root!;
        XElement form = FormElement(document, formName) ??
                        AddChild(root, new XElement("HotkeySettings", new XAttribute("Name", formName)));
        XElement commands = form.Element("Commands") ?? AddChild(form, new XElement("Commands"));

        foreach (UpstreamHotkey hotkey in hotkeys)
        {
            // By code, as upstream matches them: a renamed script keeps its hotkey and is written under its new name.
            string code = hotkey.CommandCode.ToString(CultureInfo.InvariantCulture);
            commands.Elements("HotkeyCommand")
                .FirstOrDefault(element => element.Attribute("CommandCode")?.Value == code)?.Remove();
            commands.Add(new XElement("HotkeyCommand",
                new XAttribute("CommandCode", hotkey.CommandCode),
                new XAttribute("Name", hotkey.Name),
                new XAttribute("KeyData", KeysText(hotkey.KeyData))));
        }

        return document.Declaration + Environment.NewLine + root;
    }

    /// <summary>
    ///  The serializer's text for a key: the key code's name, then the modifiers, e.g. "Space Control"; "None" for no key.
    /// </summary>
    public static string KeysText(Keys keys)
    {
        List<string> parts = [];
        Keys code = keys & Keys.KeyCode;
        if (code != Keys.None)
        {
            parts.Add(code.ToString());
        }

        foreach (Keys modifier in new[] { Keys.Shift, Keys.Control, Keys.Alt })
        {
            if ((keys & modifier) == modifier)
            {
                parts.Add(modifier.ToString());
            }
        }

        return parts.Count == 0 ? "None" : string.Join(' ', parts);
    }

    private static IEnumerable<UpstreamHotkey> ReadAll(string? serialized, string formName)
    {
        if (FormElement(Parse(serialized), formName) is not { } form)
        {
            yield break;
        }

        foreach (XElement command in form.Element("Commands")?.Elements("HotkeyCommand") ?? [])
        {
            if (command.Attribute("Name")?.Value is { Length: > 0 } name
                && int.TryParse(command.Attribute("CommandCode")?.Value, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int code)
                && TryParseKeys(command.Attribute("KeyData")?.Value ?? "None", out Keys keys))
            {
                yield return new UpstreamHotkey(code, name, keys);
            }
        }
    }

    private static bool TryParseKeys(string text, out Keys keys)
    {
        keys = Keys.None;
        foreach (string name in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!Enum.TryParse(name, ignoreCase: false, out Keys part))
            {
                return false;
            }

            keys |= part;
        }

        return true;
    }

    private static XDocument? Parse(string? serialized)
    {
        if (string.IsNullOrWhiteSpace(serialized))
        {
            return null;
        }

        try
        {
            XDocument document = XDocument.Parse(serialized);
            return document.Root?.Name == "ArrayOfHotkeySettings" ? document : null;
        }
        catch (System.Xml.XmlException)
        {
            // A damaged setting is ignored, as upstream ignores it, and replaced on the next save.
            return null;
        }
    }

    private static XElement? FormElement(XDocument? document, string formName)
        => document?.Root?.Elements("HotkeySettings")
            .FirstOrDefault(element => element.Attribute("Name")?.Value == formName);

    private static XDocument NewDocument()
    {
        XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";
        XNamespace xsd = "http://www.w3.org/2001/XMLSchema";
        return new XDocument(new XDeclaration("1.0", "utf-16", null),
            new XElement("ArrayOfHotkeySettings", new XAttribute(XNamespace.Xmlns + "xsi", xsi),
                new XAttribute(XNamespace.Xmlns + "xsd", xsd)));
    }

    private static XElement AddChild(XElement parent, XElement child)
    {
        parent.Add(child);
        return child;
    }
}
