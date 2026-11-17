using Avalonia.Input;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.App;

/// <summary>
///  The Settings rows of the windows' hotkeys (<see cref="HotkeysPageViewModel"/>) from their tables, and the key capture of
///  a row's box.
/// </summary>
internal static class HotkeyEditor
{
    /// <summary>
    ///  The rows of <paramref name="table"/>'s commands, which write their changes into the table's section.
    /// </summary>
    public static HotkeyGroup Group<TCommand>(HotkeyTable<TCommand> table, string title)
        where TCommand : struct
        => new(title,
            [
                .. table.Configurable.Select(command => new HotkeyRow(command, table.Describe(command),
                    table.GestureFor(command)?.ToString() ?? "", table.DefaultGestureFor(command)?.ToString() ?? ""))
            ],
            (serialized, changed) => table.Save(serialized,
                changed.ToDictionary(row => (TCommand)row.Command, row => row.Gesture as KeyGesture)));

    /// <summary>
    ///  Keeps the combination pressed in a row's box. A key on its own (Shift, Ctrl, ...) is the start of a combination, not
    ///  a hotkey; Tab moves on as usual.
    /// </summary>
    public static void Capture(HotkeyRow row, KeyEventArgs e)
    {
        if (e.Key is Key.LeftShift or Key.RightShift or Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LWin or Key.RWin or Key.Tab)
        {
            e.Handled = e.Key != Key.Tab;
            return;
        }

        e.Handled = true;
        KeyGesture gesture = new(e.Key, e.KeyModifiers);
        row.Capture(gesture, gesture.ToString());
    }
}
