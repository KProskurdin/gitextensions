using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using GitExtensions.Extensibility.Settings;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>SettingsPageHeader</c> above a settings page: "Settings source:" and the levels the page can show, from
///  the settings in effect to the widest, joined by arrows while the effective settings are shown, and a line under them.
///  The effective and system settings are shown read-only, as upstream does.
/// </summary>
public sealed class SettingsSourceHeader : StackPanel
{
    private const string Arrow = "<<";

    // Upstream's order and texts.
    private static readonly (SettingLevel Level, string Text)[] _levelTexts =
    [
        (SettingLevel.Effective, "Effective"),
        (SettingLevel.Local, "Local for current repository"),
        (SettingLevel.Distributed, "Distributed with current repository"),
        (SettingLevel.Global, "Global for all repositories"),
        (SettingLevel.SystemWide, "System"),
    ];

    private static int _headerCount;

    private readonly Dictionary<SettingLevel, RadioButton> _buttons = [];
    private readonly List<TextBlock> _arrows = [];
    private SettingLevel _level;
    private bool _showingLevel;

    public SettingsSourceHeader(IReadOnlyList<SettingLevel> levels)
    {
        Levels = [.. _levelTexts.Select(level => level.Level).Where(levels.Contains)];
        Margin = new Thickness(0, 4, 0, 8);
        WrapPanel choices = new() { Orientation = Orientation.Horizontal };
        choices.Children.Add(new TextBlock
        {
            Text = "Settings source:",
            FontWeight = FontWeight.Bold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(3, 0, 9, 0),
        });

        string group = $"SettingsSource{Interlocked.Increment(ref _headerCount)}";
        foreach ((SettingLevel level, string text) in _levelTexts.Where(level => Levels.Contains(level.Level)))
        {
            if (_buttons.Count > 0)
            {
                TextBlock arrow = new()
                {
                    Text = Arrow, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3, 0)
                };
                _arrows.Add(arrow);
                choices.Children.Add(arrow);
            }

            RadioButton button = new() { Content = text, GroupName = group, Margin = new Thickness(3, 0) };
            button.IsCheckedChanged += (_, _) =>
            {
                if (button.IsChecked == true && !_showingLevel)
                {
                    Show(level);
                    LevelChanged?.Invoke(this, EventArgs.Empty);
                }
            };
            _buttons[level] = button;
            choices.Children.Add(button);
        }

        Children.Add(choices);

        // Upstream's linePanel under the choices.
        Border line = new() { Height = 1, Margin = new Thickness(3, 6, 3, 0) };
        line.Bind(Border.BackgroundProperty, line.GetResourceObservable("SystemControlForegroundBaseLowBrush"));
        Children.Add(line);

        Show(Levels[0]);
    }

    /// <summary>
    ///  Raised when the user chooses another level.
    /// </summary>
    public event EventHandler? LevelChanged;

    /// <summary>
    ///  The levels offered, in upstream's order.
    /// </summary>
    public IReadOnlyList<SettingLevel> Levels { get; }

    /// <summary>
    ///  The level shown; setting it chooses that level as a click does.
    /// </summary>
    public SettingLevel Level
    {
        get => _level;
        set => _buttons[value].IsChecked = true;
    }

    /// <summary>
    ///  True while the page shows settings that are not edited there: the effective or the system settings.
    /// </summary>
    public bool IsReadOnly => _level is SettingLevel.Effective or SettingLevel.SystemWide;

    private void Show(SettingLevel level)
    {
        _level = level;
        _showingLevel = true;
        _buttons[level].IsChecked = true;
        _showingLevel = false;

        // Upstream draws the arrows in the window's color unless the effective settings are shown.
        foreach (TextBlock arrow in _arrows)
        {
            arrow.Opacity = level == SettingLevel.Effective ? 1 : 0;
        }
    }
}
