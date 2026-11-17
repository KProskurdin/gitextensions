using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace GitExtensions.Xplat.Core.Settings;

/// <summary>
///  A row of the Hotkeys page: a command, its keys, and the "None" button that removes them. The view captures the keys
///  pressed in the box (<see cref="Capture"/>); a change is kept until the page is saved.
/// </summary>
/// <param name="command">The window's command, or a script's hotkey identifier.</param>
/// <param name="description">The command's label.</param>
/// <param name="text">The keys stored.</param>
/// <param name="defaultText">The default keys.</param>
public sealed partial class HotkeyRow(object command, string description, string text, string defaultText)
    : ObservableObject
{
    public object Command { get; } = command;

    public string Description { get; } = description;

    public string DefaultText { get; } = defaultText;

    [ObservableProperty]
    public partial string Text { get; private set; } = text;

    /// <summary>
    ///  True when the keys were changed since the window opened or the page was reset.
    /// </summary>
    public bool IsChanged { get; private set; }

    /// <summary>
    ///  The keys pressed, as the view made them; null for none.
    /// </summary>
    public object? Gesture { get; private set; }

    /// <summary>
    ///  Keeps the keys pressed in the row's box.
    /// </summary>
    public void Capture(object? gesture, string text)
    {
        Gesture = gesture;
        Text = text;
        IsChanged = true;
    }

    internal void ShowDefault()
    {
        Gesture = null;
        Text = DefaultText;
        IsChanged = false;
    }

    [RelayCommand]
    private void Clear() => Capture(null, "");
}

/// <summary>
///  One window's hotkeys on the Hotkeys page.
/// </summary>
/// <param name="title">The window's name.</param>
/// <param name="rows">A row per command whose hotkey can be changed.</param>
/// <param name="save">Writes the changed rows into upstream's serialized hotkeys.</param>
public sealed class HotkeyGroup(string title, IReadOnlyList<HotkeyRow> rows,
    Func<string?, IReadOnlyList<HotkeyRow>, string?> save)
{
    public string Title { get; } = title;

    public IReadOnlyList<HotkeyRow> Rows { get; } = rows;

    /// <summary>
    ///  <paramref name="serializedHotkeys"/> with this window's changes written in, or unchanged when there are none.
    /// </summary>
    public string? Apply(string? serializedHotkeys)
        => Rows.Where(row => row.IsChanged).ToList() is { Count: > 0 } changed
            ? save(serializedHotkeys, changed)
            : serializedHotkeys;
}

/// <summary>
///  Upstream's <c>HotkeysSettingsPage</c>: per window its commands' hotkeys, and "Reset all Hotkeys to defaults".
/// </summary>
public sealed partial class HotkeysPageViewModel(IEnumerable<HotkeyGroup> groups) : ObservableObject
{
    /// <summary>
    ///  The windows that have hotkeys to change.
    /// </summary>
    public IReadOnlyList<HotkeyGroup> Groups { get; } = [.. groups.Where(group => group.Rows.Count > 0)];

    /// <summary>
    ///  True once the hotkeys were reset: then nothing stored is kept on save, not even the hotkeys of what the shell does
    ///  not have.
    /// </summary>
    public bool IsReset { get; private set; }

    /// <summary>
    ///  Upstream's hotkeys to store: <paramref name="stored"/> (nothing after a reset) with every change written in.
    /// </summary>
    public string? Apply(string? stored)
        => Groups.Aggregate(IsReset ? null : stored, (hotkeys, group) => group.Apply(hotkeys));

    // Upstream's bResetToDefaults_Click: the rows show the defaults; only Save stores it, as upstream's page.
    [RelayCommand]
    private void ResetToDefaults()
    {
        IsReset = true;
        foreach (HotkeyRow row in Groups.SelectMany(group => group.Rows))
        {
            row.ShowDefault();
        }
    }
}
