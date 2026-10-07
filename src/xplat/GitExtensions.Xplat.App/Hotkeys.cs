using System.Globalization;
using System.Text;
using Avalonia.Input;
using GitExtensions.Xplat.Core.Scripts;
using GitExtensions.Xplat.Core.Settings;
using WinFormsKeys = System.Windows.Forms.Keys;

namespace GitExtensions.Xplat.App;

/// <summary>
///  The main window's commands that have a hotkey.
/// </summary>
public enum BrowseCommand
{
    OpenRepository,
    CloseRepository,
    Commit,
    Refresh,
    Pull,
    Push,
    QuickPull,
    QuickPush,
    QuickFetch,
    Terminal,
    Settings,
    FocusFilter,
    FocusRevisionGrid,
    FocusLeftPanel,
    CreateBranch,
    CreateTag,
    Merge,
    Rebase,
    Stash,
    StashPop,
}

/// <summary>
///  The commit window's commands that have a hotkey (upstream <c>FormCommit.Command</c>).
/// </summary>
public enum CommitCommand
{
    FocusUnstagedFiles,
    FocusSelectedDiff,
    FocusStagedFiles,
    FocusCommitMessage,
    StageAll,
    OpenWithDifftool,
    Refresh,
    SelectNext,
    SelectPrevious,
}

/// <summary>
///  The revision grid's commands that have a hotkey (upstream <c>RevisionGridControl.Command</c>); they act while the grid
///  has the focus.
/// </summary>
public enum GridCommand
{
    GoToParent,
    GoToChild,
    SelectCurrentRevision,
    RevisionFilter,
    ResetRevisionFilter,
    ShowAllBranches,
    ShowCurrentBranchOnly,
    ToggleHideMergeCommits,
    ShowFirstParent,
}

/// <summary>
///  The diff view's commands that have a hotkey (upstream <c>FileViewer.Command</c>); they act while the diff has the focus.
/// </summary>
public enum DiffCommand
{
    NextChange,
    PreviousChange,
    StageLines,
    UnstageLines,
    IgnoreAllWhitespace,
    IncreaseContext,
    DecreaseContext,
    ShowEntireFile,
}

/// <summary>
///  The left panel's commands that have a hotkey (upstream <c>RepoObjectsTree.Command</c>).
/// </summary>
public enum LeftPanelCommand
{
    Delete,
}

/// <summary>
///  The conflicts window's commands that have a hotkey (upstream <c>FormResolveConflicts.Commands</c>).
/// </summary>
public enum ConflictsCommand
{
    Merge,
    Rescan,
    ChooseRemote,
    ChooseLocal,
}

/// <summary>
///  The hotkeys of one window: upstream's defaults (<c>HotkeySettingsManager</c>, the window's section), replaced by what
///  the user stored in upstream's <c>SerializedHotkeys</c> setting under the same section, so a hotkey changed in either app
///  applies in both. Ctrl becomes Cmd on macOS, the platform's command modifier.
/// </summary>
public sealed class HotkeyTable<TCommand>
    where TCommand : struct
{
    private readonly string _section;
    private readonly IReadOnlyDictionary<TCommand, (int Code, string Name)> _upstreamCommands;
    private readonly IReadOnlyDictionary<TCommand, WinFormsKeys> _defaults;
    private readonly Func<TCommand, string>? _describe;
    private Dictionary<TCommand, WinFormsKeys> _current;

    /// <param name="section">Upstream's <c>HotkeySettingsName</c> of the window.</param>
    /// <param name="upstreamCommands">Upstream's command code and name of each command the user can change.</param>
    /// <param name="defaults">Upstream's default keys; a command missing from <paramref name="upstreamCommands"/> keeps its key.</param>
    /// <param name="describe">The command's label in Settings; by default its enum name in words.</param>
    public HotkeyTable(string section, IReadOnlyDictionary<TCommand, (int Code, string Name)> upstreamCommands,
        IReadOnlyDictionary<TCommand, WinFormsKeys> defaults, Func<TCommand, string>? describe = null)
    {
        _section = section;
        _upstreamCommands = upstreamCommands;
        _defaults = defaults;
        _describe = describe;
        _current = new Dictionary<TCommand, WinFormsKeys>(defaults);
    }

    /// <summary>
    ///  The commands whose hotkey can be changed, i.e. those upstream stores.
    /// </summary>
    public IReadOnlyList<TCommand> Configurable => [.. _upstreamCommands.Keys];

    /// <summary>
    ///  The command's label in Settings: "QuickFetch" reads as "Quick fetch".
    /// </summary>
    public string Describe(TCommand command)
    {
        if (_describe is not null)
        {
            return _describe(command);
        }

        string name = command.ToString() ?? "";
        StringBuilder text = new(name.Length + 4);
        for (int i = 0; i < name.Length; i++)
        {
            text.Append(i > 0 && char.IsUpper(name[i]) ? " " + char.ToLowerInvariant(name[i]) : name[i]);
        }

        return text.ToString();
    }

    /// <summary>
    ///  Uses upstream's defaults, replaced by this section's hotkeys in <paramref name="serializedHotkeys"/>. A stored hotkey
    ///  is matched by its command code, as upstream matches it.
    /// </summary>
    public void Load(string? serializedHotkeys)
    {
        Dictionary<TCommand, WinFormsKeys> current = new(_defaults);
        IReadOnlyDictionary<int, UpstreamHotkey> stored = UpstreamHotkeys.ReadByCode(serializedHotkeys, _section);
        foreach ((TCommand command, (int code, _)) in _upstreamCommands)
        {
            if (stored.TryGetValue(code, out UpstreamHotkey? hotkey))
            {
                current[command] = hotkey.KeyData;
            }
        }

        _current = current;
    }

    /// <summary>
    ///  <paramref name="serializedHotkeys"/> with the given commands' keys changed, ready to store in the upstream setting.
    /// </summary>
    public string Save(string? serializedHotkeys, IReadOnlyDictionary<TCommand, KeyGesture?> changes)
        => UpstreamHotkeys.Write(serializedHotkeys, _section,
            changes.Where(change => _upstreamCommands.ContainsKey(change.Key))
                .Select(change => new UpstreamHotkey(_upstreamCommands[change.Key].Code,
                    _upstreamCommands[change.Key].Name,
                    change.Value is { } gesture ? Hotkeys.ToWinFormsKeys(gesture) : WinFormsKeys.None)));

    /// <summary>
    ///  The command's hotkey, or null when it has none.
    /// </summary>
    public KeyGesture? GestureFor(TCommand command)
        => _current.TryGetValue(command, out WinFormsKeys keys) ? Hotkeys.ToGesture(keys) : null;

    /// <summary>
    ///  The command whose hotkey is <paramref name="e"/>, or null.
    /// </summary>
    public TCommand? Match(KeyEventArgs e)
    {
        foreach (TCommand command in _current.Keys)
        {
            if (GestureFor(command) is { } gesture && gesture.Matches(e))
            {
                return command;
            }
        }

        return null;
    }
}

/// <summary>
///  The hotkey tables of the windows that have upstream hotkeys, and the key conversions between Avalonia and the WinForms
///  values upstream stores. The members without a table name act on the browse window's table.
/// </summary>
public static class Hotkeys
{
    /// <summary>
    ///  The browse window (upstream FormBrowse). Refresh is not a FormBrowse command upstream (F5 is handled by the grid),
    ///  so it keeps its fixed key.
    /// </summary>
    public static HotkeyTable<BrowseCommand> Browse { get; } = new(UpstreamHotkeys.BrowseFormName,
        new Dictionary<BrowseCommand, (int, string)>
        {
            [BrowseCommand.OpenRepository] = (45, "OpenRepo"),
            [BrowseCommand.CloseRepository] = (15, "CloseRepository"),
            [BrowseCommand.Commit] = (7, "Commit"),
            [BrowseCommand.Pull] = (39, "PullOrFetch"),
            [BrowseCommand.Push] = (40, "Push"),
            [BrowseCommand.QuickPull] = (12, "QuickPull"),
            [BrowseCommand.QuickPush] = (13, "QuickPush"),
            [BrowseCommand.QuickFetch] = (11, "QuickFetch"),
            [BrowseCommand.Terminal] = (0, "GitBash"),
            [BrowseCommand.Settings] = (20, "OpenSettings"),
            [BrowseCommand.FocusFilter] = (18, "FocusFilter"),
            [BrowseCommand.FocusRevisionGrid] = (3, "FocusRevisionGrid"),
            [BrowseCommand.FocusLeftPanel] = (25, "FocusLeftPanel"),
            [BrowseCommand.CreateBranch] = (41, "CreateBranch"),
            [BrowseCommand.CreateTag] = (43, "CreateTag"),
            [BrowseCommand.Merge] = (42, "MergeBranches"),
            [BrowseCommand.Rebase] = (44, "Rebase"),
            [BrowseCommand.Stash] = (16, "Stash"),
            [BrowseCommand.StashPop] = (17, "StashPop"),
        },
        new Dictionary<BrowseCommand, WinFormsKeys>
        {
            [BrowseCommand.OpenRepository] = WinFormsKeys.Control | WinFormsKeys.O,
            [BrowseCommand.CloseRepository] = WinFormsKeys.Control | WinFormsKeys.W,
            [BrowseCommand.Commit] = WinFormsKeys.Control | WinFormsKeys.Space,
            [BrowseCommand.Refresh] = WinFormsKeys.F5,
            [BrowseCommand.Pull] = WinFormsKeys.Control | WinFormsKeys.Down,
            [BrowseCommand.Push] = WinFormsKeys.Control | WinFormsKeys.Up,
            [BrowseCommand.QuickPull] = WinFormsKeys.Control | WinFormsKeys.Shift | WinFormsKeys.P,
            [BrowseCommand.QuickPush] = WinFormsKeys.Control | WinFormsKeys.Shift | WinFormsKeys.Up,
            [BrowseCommand.QuickFetch] = WinFormsKeys.Control | WinFormsKeys.Shift | WinFormsKeys.Down,
            [BrowseCommand.Terminal] = WinFormsKeys.Control | WinFormsKeys.G,
            [BrowseCommand.Settings] = WinFormsKeys.Control | WinFormsKeys.Oemcomma,
            [BrowseCommand.FocusFilter] = WinFormsKeys.Control | WinFormsKeys.E,
            [BrowseCommand.FocusRevisionGrid] = WinFormsKeys.Control | WinFormsKeys.D1,
            [BrowseCommand.FocusLeftPanel] = WinFormsKeys.Control | WinFormsKeys.D0,
            [BrowseCommand.CreateBranch] = WinFormsKeys.Control | WinFormsKeys.B,
            [BrowseCommand.CreateTag] = WinFormsKeys.Control | WinFormsKeys.T,
            [BrowseCommand.Merge] = WinFormsKeys.Control | WinFormsKeys.M,
            [BrowseCommand.Rebase] = WinFormsKeys.Control | WinFormsKeys.Shift | WinFormsKeys.E,
            [BrowseCommand.Stash] = WinFormsKeys.Control | WinFormsKeys.Alt | WinFormsKeys.Up,
            [BrowseCommand.StashPop] = WinFormsKeys.Control | WinFormsKeys.Alt | WinFormsKeys.Down,
        });

    /// <summary>
    ///  The commit window (upstream FormCommit): the commands the new shell's commit window has, with upstream's codes and
    ///  defaults. Upstream's alternative keys (Alt+arrows for next and previous) are not ported.
    /// </summary>
    public static HotkeyTable<CommitCommand> Commit { get; } = new(UpstreamHotkeys.CommitFormName,
        new Dictionary<CommitCommand, (int, string)>
        {
            [CommitCommand.FocusUnstagedFiles] = (2, "FocusUnstagedFiles"),
            [CommitCommand.FocusSelectedDiff] = (3, "FocusSelectedDiff"),
            [CommitCommand.FocusStagedFiles] = (4, "FocusStagedFiles"),
            [CommitCommand.FocusCommitMessage] = (5, "FocusCommitMessage"),
            [CommitCommand.StageAll] = (11, "StageAll"),
            [CommitCommand.OpenWithDifftool] = (12, "OpenWithDifftool"),
            [CommitCommand.Refresh] = (18, "Refresh"),
            [CommitCommand.SelectNext] = (19, "SelectNext"),
            [CommitCommand.SelectPrevious] = (22, "SelectPrevious"),
        },
        new Dictionary<CommitCommand, WinFormsKeys>
        {
            [CommitCommand.FocusUnstagedFiles] = WinFormsKeys.Control | WinFormsKeys.D1,
            [CommitCommand.FocusSelectedDiff] = WinFormsKeys.Control | WinFormsKeys.D2,
            [CommitCommand.FocusStagedFiles] = WinFormsKeys.Control | WinFormsKeys.D3,
            [CommitCommand.FocusCommitMessage] = WinFormsKeys.Control | WinFormsKeys.D4,
            [CommitCommand.StageAll] = WinFormsKeys.Control | WinFormsKeys.S,
            [CommitCommand.OpenWithDifftool] = WinFormsKeys.F3,
            [CommitCommand.Refresh] = WinFormsKeys.F5,
            [CommitCommand.SelectNext] = WinFormsKeys.Control | WinFormsKeys.N,
            [CommitCommand.SelectPrevious] = WinFormsKeys.Control | WinFormsKeys.P,
        });

    /// <summary>
    ///  The revision grid (upstream RevisionGridControl, section "RevisionGrid"): the commands the new shell's grid has.
    /// </summary>
    public static HotkeyTable<GridCommand> Grid { get; } = new(UpstreamHotkeys.RevisionGridName,
        new Dictionary<GridCommand, (int, string)>
        {
            [GridCommand.GoToParent] = (14, "GoToParent"),
            [GridCommand.GoToChild] = (15, "GoToChild"),
            [GridCommand.SelectCurrentRevision] = (19, "SelectCurrentRevision"),
            [GridCommand.RevisionFilter] = (1, "RevisionFilter"),
            [GridCommand.ResetRevisionFilter] = (36, "ResetRevisionFilter"),
            [GridCommand.ShowAllBranches] = (9, "ShowAllBranches"),
            [GridCommand.ShowCurrentBranchOnly] = (10, "ShowCurrentBranchOnly"),
            [GridCommand.ToggleHideMergeCommits] = (8, "ToggleHideMergeCommits"),
            [GridCommand.ShowFirstParent] = (13, "ShowFirstParent"),
        },
        new Dictionary<GridCommand, WinFormsKeys>
        {
            [GridCommand.GoToParent] = WinFormsKeys.Control | WinFormsKeys.P,
            [GridCommand.GoToChild] = WinFormsKeys.Control | WinFormsKeys.N,
            [GridCommand.SelectCurrentRevision] = WinFormsKeys.Control | WinFormsKeys.Shift | WinFormsKeys.C,
            [GridCommand.RevisionFilter] = WinFormsKeys.Control | WinFormsKeys.I,
            [GridCommand.ResetRevisionFilter] = WinFormsKeys.Control | WinFormsKeys.Shift | WinFormsKeys.I,
            [GridCommand.ShowAllBranches] = WinFormsKeys.Control | WinFormsKeys.Shift | WinFormsKeys.A,
            [GridCommand.ShowCurrentBranchOnly] = WinFormsKeys.Control | WinFormsKeys.Shift | WinFormsKeys.U,
            [GridCommand.ToggleHideMergeCommits] = WinFormsKeys.Control | WinFormsKeys.Shift | WinFormsKeys.M,
            [GridCommand.ShowFirstParent] = WinFormsKeys.Control | WinFormsKeys.Shift | WinFormsKeys.S,
        });

    /// <summary>
    ///  The diff view (upstream FileViewer, section "FileViewer").
    /// </summary>
    public static HotkeyTable<DiffCommand> Diff { get; } = new(UpstreamHotkeys.FileViewerName,
        new Dictionary<DiffCommand, (int, string)>
        {
            [DiffCommand.NextChange] = (6, "NextChange"),
            [DiffCommand.PreviousChange] = (7, "PreviousChange"),
            [DiffCommand.StageLines] = (12, "StageLines"),
            [DiffCommand.UnstageLines] = (13, "UnstageLines"),
            [DiffCommand.IgnoreAllWhitespace] = (15, "IgnoreAllWhitespace"),
            [DiffCommand.IncreaseContext] = (2, "IncreaseNumberOfVisibleLines"),
            [DiffCommand.DecreaseContext] = (3, "DecreaseNumberOfVisibleLines"),
            [DiffCommand.ShowEntireFile] = (4, "ShowEntireFile"),
        },
        new Dictionary<DiffCommand, WinFormsKeys>
        {
            [DiffCommand.NextChange] = WinFormsKeys.Alt | WinFormsKeys.Down,
            [DiffCommand.PreviousChange] = WinFormsKeys.Alt | WinFormsKeys.Up,
            [DiffCommand.StageLines] = WinFormsKeys.S,
            [DiffCommand.UnstageLines] = WinFormsKeys.U,
            [DiffCommand.IgnoreAllWhitespace] = WinFormsKeys.Control | WinFormsKeys.Shift | WinFormsKeys.W,
            [DiffCommand.IncreaseContext] = WinFormsKeys.Control | WinFormsKeys.Oemplus,
            [DiffCommand.DecreaseContext] = WinFormsKeys.Control | WinFormsKeys.OemMinus,
            [DiffCommand.ShowEntireFile] = WinFormsKeys.Control | WinFormsKeys.E,
        });

    /// <summary>
    ///  The left panel (upstream RepoObjectsTree, section "LeftPanel").
    /// </summary>
    public static HotkeyTable<LeftPanelCommand> LeftPanel { get; } = new(UpstreamHotkeys.LeftPanelName,
        new Dictionary<LeftPanelCommand, (int, string)> { [LeftPanelCommand.Delete] = (0, "Delete") },
        new Dictionary<LeftPanelCommand, WinFormsKeys> { [LeftPanelCommand.Delete] = WinFormsKeys.Delete });

    /// <summary>
    ///  The conflicts window (upstream FormResolveConflicts, section "FormMergeConflicts"). Choose base is not ported: the
    ///  window has no base choice.
    /// </summary>
    public static HotkeyTable<ConflictsCommand> Conflicts { get; } = new(UpstreamHotkeys.ResolveConflictsName,
        new Dictionary<ConflictsCommand, (int, string)>
        {
            [ConflictsCommand.Merge] = (0, "Merge"),
            [ConflictsCommand.Rescan] = (1, "Rescan"),
            [ConflictsCommand.ChooseRemote] = (2, "ChooseRemote"),
            [ConflictsCommand.ChooseLocal] = (3, "ChooseLocal"),
        },
        new Dictionary<ConflictsCommand, WinFormsKeys>
        {
            [ConflictsCommand.Merge] = WinFormsKeys.M,
            [ConflictsCommand.Rescan] = WinFormsKeys.F5,
            [ConflictsCommand.ChooseRemote] = WinFormsKeys.R,
            [ConflictsCommand.ChooseLocal] = WinFormsKeys.L,
        });

    /// <summary>
    ///  The user scripts (upstream section "Scripts"): one command per named script, its <c>HotkeyCommandIdentifier</c>, with
    ///  no key by default. Upstream appends these to every window's hotkeys; rebuilt by <see cref="Load"/> from the stored
    ///  scripts.
    /// </summary>
    public static HotkeyTable<int> Scripts { get; private set; } = ScriptTable([]);

    /// <summary>
    ///  Ctrl, or Cmd on macOS.
    /// </summary>
    public static KeyModifiers CommandModifier => OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;

    /// <summary>
    ///  The browse window's commands whose hotkey can be changed.
    /// </summary>
    public static IReadOnlyList<BrowseCommand> Configurable => Browse.Configurable;

    /// <summary>
    ///  Loads every window's hotkeys from upstream's setting, and the scripts' hotkeys for the stored scripts.
    /// </summary>
    public static void Load(string? serializedHotkeys)
    {
        Browse.Load(serializedHotkeys);
        Commit.Load(serializedHotkeys);
        Grid.Load(serializedHotkeys);
        Diff.Load(serializedHotkeys);
        LeftPanel.Load(serializedHotkeys);
        Conflicts.Load(serializedHotkeys);
        Scripts = ScriptTable(AppServices.Scripts.Load());
        Scripts.Load(serializedHotkeys);
    }

    /// <summary>
    ///  The scripts' hotkey table as upstream's <c>LoadScriptHotkeys</c> builds it: every script with a name, enabled or
    ///  not, under its display name.
    /// </summary>
    public static HotkeyTable<int> ScriptTable(IReadOnlyList<ScriptDefinition> scripts)
    {
        Dictionary<int, (int, string)> commands = [];
        Dictionary<int, WinFormsKeys> defaults = [];
        foreach (ScriptDefinition script in scripts.Where(script => !string.IsNullOrEmpty(script.Name)))
        {
            commands[script.HotkeyCommandIdentifier] = (script.HotkeyCommandIdentifier, script.DisplayName);
            defaults[script.HotkeyCommandIdentifier] = WinFormsKeys.None;
        }

        return new HotkeyTable<int>(UpstreamHotkeys.ScriptsName, commands, defaults,
            id => commands.TryGetValue(id, out (int, string Name) command)
                ? command.Name
                : id.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    ///  <paramref name="serializedHotkeys"/> with the given browse commands' keys changed.
    /// </summary>
    public static string Save(string? serializedHotkeys, IReadOnlyDictionary<BrowseCommand, KeyGesture?> changes)
        => Browse.Save(serializedHotkeys, changes);

    public static KeyGesture? GestureFor(BrowseCommand command) => Browse.GestureFor(command);

    public static BrowseCommand? Match(KeyEventArgs e) => Browse.Match(e);

    /// <summary>
    ///  A WinForms key value as an Avalonia gesture, or null when the key has no Avalonia equivalent (or is none).
    /// </summary>
    public static KeyGesture? ToGesture(WinFormsKeys keys)
    {
        WinFormsKeys code = keys & WinFormsKeys.KeyCode;
        if (code == WinFormsKeys.None || !Enum.TryParse(code.ToString(), ignoreCase: true, out Key key))
        {
            return null;
        }

        KeyModifiers modifiers = KeyModifiers.None;
        if (keys.HasFlag(WinFormsKeys.Control))
        {
            modifiers |= CommandModifier;
        }

        if (keys.HasFlag(WinFormsKeys.Shift))
        {
            modifiers |= KeyModifiers.Shift;
        }

        if (keys.HasFlag(WinFormsKeys.Alt))
        {
            modifiers |= KeyModifiers.Alt;
        }

        return new KeyGesture(key, modifiers);
    }

    /// <summary>
    ///  An Avalonia gesture as the WinForms key value upstream stores; the command modifier is stored as Control.
    /// </summary>
    public static WinFormsKeys ToWinFormsKeys(KeyGesture gesture)
    {
        if (!Enum.TryParse(gesture.Key.ToString(), ignoreCase: true, out WinFormsKeys keys))
        {
            return WinFormsKeys.None;
        }

        if (gesture.KeyModifiers.HasFlag(CommandModifier))
        {
            keys |= WinFormsKeys.Control;
        }

        if (gesture.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            keys |= WinFormsKeys.Shift;
        }

        if (gesture.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            keys |= WinFormsKeys.Alt;
        }

        return keys;
    }
}
