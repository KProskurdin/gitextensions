using System.Diagnostics;
using System.Xml;
using System.Xml.Serialization;
using GitCommands;
using GitUI.ScriptsEngine;

namespace GitExtensions.Xplat.Core.Scripts;

/// <summary>
///  The user scripts, kept where upstream keeps them.
/// </summary>
public interface IScriptStore
{
    IReadOnlyList<ScriptDefinition> Load();

    void Save(IReadOnlyList<ScriptDefinition> scripts);
}

/// <summary>
///  Reads and writes upstream's <c>ownScripts</c> setting through <see cref="AppSettings.OwnScripts"/>.
/// </summary>
public sealed class UpstreamScriptStore : IScriptStore
{
    public IReadOnlyList<ScriptDefinition> Load() => ScriptsXml.Read(AppSettings.OwnScripts);

    public void Save(IReadOnlyList<ScriptDefinition> scripts)
    {
        AppSettings.OwnScripts = ScriptsXml.Write(scripts);
        AppSettings.SaveSettings();
    }
}

/// <summary>
///  Keeps the scripts for the life of the process. Tests use it so they never write the user's settings file.
/// </summary>
public sealed class InMemoryScriptStore(IReadOnlyList<ScriptDefinition>? scripts = null) : IScriptStore
{
    private IReadOnlyList<ScriptDefinition> _scripts = scripts ?? [];

    public IReadOnlyList<ScriptDefinition> Load() => _scripts;

    public void Save(IReadOnlyList<ScriptDefinition> scripts) => _scripts = [.. scripts];
}

/// <summary>
///  Upstream's scripts XML (<c>ScriptsManager.SerializeIntoXml</c> and <c>DeserializeFromXml</c>): the XmlSerializer output of
///  the script list, upstream's default scripts when nothing is stored, and the older separator format.
/// </summary>
public static class ScriptsXml
{
    /// <summary>
    ///  Upstream's <c>MinimumUserScriptID</c>: scripts' hotkey identifiers start here.
    /// </summary>
    public const int MinimumScriptId = 9000;

    private static readonly XmlSerializer _serializer = new(typeof(List<ScriptDefinition>));

    public static IReadOnlyList<ScriptDefinition> Read(string? xml)
    {
        if (string.IsNullOrEmpty(xml))
        {
            return Defaults();
        }

        List<ScriptDefinition> scripts;
        try
        {
            using StringReader reader = new(xml);
            using XmlReader xmlReader = XmlReader.Create(reader);
            scripts = (List<ScriptDefinition>)_serializer.Deserialize(xmlReader)!;
        }
        catch (InvalidOperationException ex)
        {
            Trace.WriteLine(ex.Message);
            scripts = ReadOldFormat(xml);
        }

        FixAmbiguousIds(scripts);
        return scripts;
    }

    public static string Write(IReadOnlyList<ScriptDefinition> scripts)
    {
        XmlWriterSettings settings = new() { Indent = true };
        using StringWriter writer = new();
        using (XmlWriter xmlWriter = XmlWriter.Create(writer, settings))
        {
            _serializer.Serialize(xmlWriter, scripts.ToList());
        }

        return writer.ToString();
    }

    /// <summary>
    ///  The next free hotkey identifier, as upstream's <c>NextHotkeyCommandIdentifier</c>.
    /// </summary>
    public static int NextId(IReadOnlyList<ScriptDefinition> scripts)
        => scripts.Count == 0 ? MinimumScriptId : Math.Max(MinimumScriptId - 1, scripts.Max(script => script.HotkeyCommandIdentifier)) + 1;

    // Upstream gives a script whose identifier is already taken a new one, so each hotkey reaches one script.
    private static void FixAmbiguousIds(List<ScriptDefinition> scripts)
    {
        HashSet<int> ids = [];
        foreach (ScriptDefinition script in scripts)
        {
            if (!ids.Add(script.HotkeyCommandIdentifier))
            {
                script.HotkeyCommandIdentifier = NextId(scripts);
                ids.Add(script.HotkeyCommandIdentifier);
            }
        }
    }

    private static List<ScriptDefinition> ReadOldFormat(string text)
    {
        const string paramSeparator = "<_PARAM_SEPARATOR_>";
        const string scriptSeparator = "<_SCRIPT_SEPARATOR_>";
        List<ScriptDefinition> scripts = [];
        if (!text.Contains(paramSeparator) && !text.Contains(scriptSeparator))
        {
            return scripts;
        }

        foreach (string script in text.Split([scriptSeparator], StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parameters = script.Split([paramSeparator], StringSplitOptions.None);
            scripts.Add(new ScriptDefinition
            {
                Name = parameters[0],
                Command = parameters.ElementAtOrDefault(1),
                Arguments = parameters.ElementAtOrDefault(2),
                AddToRevisionGridContextMenu = parameters.ElementAtOrDefault(3) == "yes",
                Enabled = true,
                RunInBackground = false,
            });
        }

        return scripts;
    }

    // Upstream's default scripts (ScriptsManager.GetDefaultScripts), all disabled, as upstream ships them.
    private static List<ScriptDefinition> Defaults() =>
    [
        new() { HotkeyCommandIdentifier = 9000, Name = "Fetch changes after commit", Command = "git", Arguments = "fetch", AskConfirmation = true, OnEvent = ScriptEvent.AfterCommit, Enabled = false },
        new() { HotkeyCommandIdentifier = 9001, Name = "Update submodules after pull", Command = "git", Arguments = "submodule update --init --recursive", AskConfirmation = true, OnEvent = ScriptEvent.AfterPull, Enabled = false },
        new()
        {
            HotkeyCommandIdentifier = 9005, Icon = "EditFile", Name = "&Open in VS Code", Command = "bash",
            Arguments = "-c '"
                        + @"if [ -z ""{SelectedRelativePaths}"" ]; then code .; "
                        + @"elif [ -d ""{SelectedRelativePaths}"" ]; then code {{SelectedRelativePaths}}; "
                        + @"elif [ ! -f ""{SelectedRelativePaths}"" ]; then code . --goto {{SelectedRelativePaths}}; "
                        + @"else code . --goto {{SelectedRelativePaths}}:{LineNumber}:{ColumnNumber}; "
                        + @"fi'",
            RunInBackground = true, OnEvent = ScriptEvent.ShowInUserMenuBar, Enabled = false,
        },
        new() { HotkeyCommandIdentifier = 9002, Name = "&Example", Command = @"c:\windows\system32\calc.exe", Arguments = "", OnEvent = ScriptEvent.ShowInUserMenuBar, Enabled = false },
        new() { HotkeyCommandIdentifier = 9003, Name = "Open on GitHub", Command = "{openurl}", Arguments = "https://github.com{cDefaultRemotePathFromUrl}/commit/{sHash}", AddToRevisionGridContextMenu = true, Enabled = false },
        new() { HotkeyCommandIdentifier = 9004, Name = "Fetch All Submodules", Command = "git", Arguments = "submodule foreach --recursive git fetch --all", AddToRevisionGridContextMenu = true, Enabled = false },
        new() { HotkeyCommandIdentifier = 9006, Name = "Convert workspace file to LF", Command = "bash.exe", Arguments = "-c 'dos2unix.exe {{SelectedRelativePaths}}'", RunInBackground = true, Enabled = false },
        new() { HotkeyCommandIdentifier = 9007, Name = "Convert workspace file to CRLF", Command = "bash.exe", Arguments = "-c 'unix2dos.exe {{SelectedRelativePaths}}'", RunInBackground = true, Enabled = false },
    ];
}
