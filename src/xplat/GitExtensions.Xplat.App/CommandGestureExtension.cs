using System.Globalization;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

namespace GitExtensions.Xplat.App;

/// <summary>
///  A key with the platform's command modifier (Ctrl, or Cmd on macOS), for a menu item's <c>InputGesture</c> in XAML, where
///  a literal <c>Ctrl+S</c> would show the wrong key on macOS.
/// </summary>
public sealed class CommandGestureExtension(Key key) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) => new KeyGesture(key, Hotkeys.CommandModifier);
}

/// <summary>
///  A text naming the platform's command modifier, for a tooltip in XAML; <c>{0}</c> in the format is its name. The key stays
///  in the text, as a gesture would call Enter "Return".
/// </summary>
public sealed class CommandShortcutTextExtension(string format) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider)
        => string.Format(CultureInfo.InvariantCulture, format, Hotkeys.CommandModifierName);
}
