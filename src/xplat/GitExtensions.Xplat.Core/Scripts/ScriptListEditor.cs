using System.Collections.ObjectModel;
using GitUI.ScriptsEngine;

namespace GitExtensions.Xplat.Core.Scripts;

/// <summary>
///  The script list of the Settings window, as upstream's <c>ScriptsSettingsPage</c> edits it: add, delete and reorder
///  scripts, and change one script's values. It works on copies, so nothing is stored until <see cref="Scripts"/> is saved.
/// </summary>
public sealed class ScriptListEditor
{
    /// <summary>
    ///  Upstream's name for a script just added.
    /// </summary>
    public const string NewScriptName = "<New Script>";

    private readonly ObservableCollection<ScriptListItem> _items;

    public ScriptListEditor(IEnumerable<ScriptDefinition> scripts)
    {
        _items = [.. scripts.Select(script => new ScriptListItem(script.Clone()))];
        Items = new ReadOnlyObservableCollection<ScriptListItem>(_items);
    }

    /// <summary>
    ///  The rows, in the order upstream runs and shows the scripts.
    /// </summary>
    public ReadOnlyObservableCollection<ScriptListItem> Items { get; }

    /// <summary>
    ///  The edited scripts, ready to store.
    /// </summary>
    public IReadOnlyList<ScriptDefinition> Scripts => [.. _items.Select(item => item.Script)];

    /// <summary>
    ///  Adds an enabled script at the end, as upstream's Add button: a hotkey identifier above every other one (and above
    ///  upstream's <c>MinimumUserScriptID</c>) and the name "&lt;New Script&gt;".
    /// </summary>
    public ScriptListItem Add()
    {
        int id = Math.Max(ScriptsXml.MinimumScriptId,
            _items.Select(item => item.Script.HotkeyCommandIdentifier).DefaultIfEmpty(0).Max()) + 1;
        ScriptListItem item =
            new(new ScriptDefinition { HotkeyCommandIdentifier = id, Name = NewScriptName, Enabled = true });
        _items.Add(item);
        return item;
    }

    public void Remove(ScriptListItem item) => _items.Remove(item);

    public bool CanMoveUp(ScriptListItem item) => _items.IndexOf(item) > 0;

    public bool CanMoveDown(ScriptListItem item)
    {
        int index = _items.IndexOf(item);
        return index >= 0 && index < _items.Count - 1;
    }

    public void MoveUp(ScriptListItem item)
    {
        if (!CanMoveUp(item))
        {
            return;
        }

        int index = _items.IndexOf(item);
        _items.Move(index, index - 1);
    }

    public void MoveDown(ScriptListItem item)
    {
        if (!CanMoveDown(item))
        {
            return;
        }

        int index = _items.IndexOf(item);
        _items.Move(index, index + 1);
    }
}

/// <summary>
///  One row of the script list: the script and the texts upstream's list shows (name, event, command line).
/// </summary>
public sealed class ScriptListItem(ScriptDefinition script) : ObservableObject
{
    private static readonly string[] _properties =
    [
        nameof(Name), nameof(Enabled), nameof(OnEvent), nameof(Command), nameof(Arguments),
        nameof(AddToRevisionGridContextMenu), nameof(AskConfirmation), nameof(RunInBackground), nameof(IsPowerShell),
        nameof(EventText), nameof(CommandLine)
    ];

    public ScriptDefinition Script { get; } = script;

    public string Name
    {
        get => Script.Name ?? "";
        set => Change(() => Script.Name = value);
    }

    public bool Enabled
    {
        get => Script.Enabled;
        set => Change(() => Script.Enabled = value);
    }

    public ScriptEvent OnEvent
    {
        get => Script.OnEvent;
        set => Change(() => Script.OnEvent = value);
    }

    public string Command
    {
        get => Script.Command ?? "";
        set => Change(() => Script.Command = value);
    }

    public string Arguments
    {
        get => Script.Arguments ?? "";
        set => Change(() => Script.Arguments = value);
    }

    public bool AddToRevisionGridContextMenu
    {
        get => Script.AddToRevisionGridContextMenu;
        set => Change(() => Script.AddToRevisionGridContextMenu = value);
    }

    public bool AskConfirmation
    {
        get => Script.AskConfirmation;
        set => Change(() => Script.AskConfirmation = value);
    }

    public bool RunInBackground
    {
        get => Script.RunInBackground;
        set => Change(() => Script.RunInBackground = value);
    }

    public bool IsPowerShell
    {
        get => Script.IsPowerShell;
        set => Change(() => Script.IsPowerShell = value);
    }

    /// <summary>
    ///  The event column of upstream's list: the enum name, as upstream shows it.
    /// </summary>
    public string EventText => Script.OnEvent.ToString();

    /// <summary>
    ///  The command column (and tooltip) of upstream's list.
    /// </summary>
    public string CommandLine => $"{Script.Command} {Script.Arguments}".Trim();

    // The list texts combine several values, so a change announces every property.
    private void Change(Action set)
    {
        set();
        foreach (string property in _properties)
        {
            RaisePropertyChanged(property);
        }
    }
}
