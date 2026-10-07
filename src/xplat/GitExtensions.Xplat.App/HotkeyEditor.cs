using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;

namespace GitExtensions.Xplat.App;

/// <summary>
///  The Settings rows of one window's hotkeys: per command its name, the keys (press a new combination in the box), and a
///  button that removes the hotkey. Changes are kept until <see cref="Apply"/>.
/// </summary>
internal sealed class HotkeyEditor<TCommand>(HotkeyTable<TCommand> table)
    where TCommand : struct
{
    private const double LabelWidth = 200;
    private const double BoxWidth = 220;

    private readonly Dictionary<TCommand, KeyGesture?> _changes = [];
    private readonly Dictionary<TCommand, TextBox> _boxes = [];

    public IReadOnlyDictionary<TCommand, TextBox> Boxes => _boxes;

    public void AddRows(Panel panel, string title)
    {
        if (table.Configurable.Count == 0)
        {
            return;
        }

        panel.Children.Add(new TextBlock { Text = title, FontWeight = Avalonia.Media.FontWeight.SemiBold, Margin = new Thickness(0, 8, 0, 2) });
        foreach (TCommand command in table.Configurable)
        {
            TextBox box = new() { IsReadOnly = true, Width = BoxWidth, Text = table.GestureFor(command)?.ToString() ?? "" };
            box.KeyDown += (_, e) => Capture(command, box, e);
            Button clear = new() { Content = "None", Margin = new Thickness(6, 0, 0, 0) };
            clear.Click += (_, _) =>
            {
                _changes[command] = null;
                box.Text = "";
            };
            _boxes[command] = box;

            DockPanel row = new() { LastChildFill = false };
            TextBlock label = new() { Text = table.Describe(command), Width = LabelWidth, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(label, Dock.Left);
            DockPanel.SetDock(box, Dock.Left);
            DockPanel.SetDock(clear, Dock.Left);
            row.Children.Add(label);
            row.Children.Add(box);
            row.Children.Add(clear);
            panel.Children.Add(row);
        }
    }

    /// <summary>
    ///  <paramref name="serializedHotkeys"/> with this window's changes written in, or unchanged when there are none.
    /// </summary>
    public string? Apply(string? serializedHotkeys)
        => _changes.Count == 0 ? serializedHotkeys : table.Save(serializedHotkeys, _changes);

    // A key on its own (Shift, Ctrl, ...) is the start of a combination, not a hotkey; Tab moves on as usual.
    private void Capture(TCommand command, TextBox box, KeyEventArgs e)
    {
        if (e.Key is Key.LeftShift or Key.RightShift or Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LWin or Key.RWin or Key.Tab)
        {
            e.Handled = e.Key != Key.Tab;
            return;
        }

        e.Handled = true;
        KeyGesture gesture = new(e.Key, e.KeyModifiers);
        _changes[command] = gesture;
        box.Text = gesture.ToString();
    }
}
