using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace GitExtensions.Xplat.App;

/// <summary>
///  A WinForms form opens with a control focused: the one it selects on load, else the first in its tab order. An Avalonia
///  window opens with none, and the dialog's buttons come first in the XAML (the footer is docked before the content), so
///  the first Tab would reach them. This focuses the control upstream's form starts on.
/// </summary>
internal static class InitialFocus
{
    private const string FooterClass = "dialogFooter";

    /// <summary>
    ///  Focuses <paramref name="control"/> when <paramref name="window"/> opens, or, when it gives none, the first control
    ///  of the content that Tab stops at; a control the window focused itself meanwhile keeps the focus. A window that
    ///  disables its controls while it loads drops the focus, so the control gets it again when it is enabled next.
    /// </summary>
    public static void Attach(Window window, Func<Control?>? control = null)
    {
        window.Opened += (_, _) =>
        {
            if (window.FocusManager.GetFocusedElement() is not null
                || (control?.Invoke() ?? FirstTabStop(window)) is not { } target)
            {
                return;
            }

            target.Focus();
            target.PropertyChanged += Refocus;

            void Refocus(object? sender, AvaloniaPropertyChangedEventArgs e)
            {
                if (e.Property == InputElement.IsEffectivelyEnabledProperty && target.IsEffectivelyEnabled)
                {
                    target.PropertyChanged -= Refocus;
                    if (window.FocusManager.GetFocusedElement() is null)
                    {
                        target.Focus();
                    }
                }
            }
        };
    }

    private static Control? FirstTabStop(Window window)
        => window.GetVisualDescendants().OfType<Control>().FirstOrDefault(control =>
            control is { Focusable: true, IsEffectivelyVisible: true }
            && KeyboardNavigation.GetIsTabStop(control)
            && !control.GetVisualAncestors().OfType<Border>().Any(border => border.Classes.Contains(FooterClass)));
}
