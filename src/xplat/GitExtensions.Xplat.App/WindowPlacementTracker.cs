using Avalonia;
using Avalonia.Controls;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Restores a window's size, position and maximized state when it is created and saves them when it closes, as upstream's
///  <c>WindowPositionManager</c> does for its forms.
/// </summary>
internal static class WindowPlacementTracker
{
    private const double StandardDpi = 96;

    // Like upstream, a saved position is used only when enough of the window would be on a screen to grab it.
    private const int MinimumVisibleWidth = 100;
    private const int MinimumVisibleHeight = 50;

    /// <summary>
    ///  Call from the window's constructor, before it is shown.
    /// </summary>
    public static void Attach(Window window, string name)
    {
        IWindowPlacementStore store = AppServices.WindowPlacements;
        PixelRect? normalBounds = null;

        if (store.Load(name) is { } placement)
        {
            Apply(window, placement);
        }

        window.PositionChanged += (_, _) => RememberNormalBounds();
        window.PropertyChanged += (_, e) =>
        {
            if (e.Property == TopLevel.ClientSizeProperty)
            {
                RememberNormalBounds();
            }
        };
        window.Closing += (_, _) =>
        {
            RememberNormalBounds();
            if (normalBounds is not { } bounds)
            {
                return;
            }

            store.Save(name, new WindowPlacement(bounds.X, bounds.Y, bounds.Width, bounds.Height,
                (int)Math.Round(StandardDpi * window.DesktopScaling), window.WindowState == WindowState.Maximized));
        };

        return;

        // Maximized and minimized windows report the screen's size; the size to restore is the last normal one.
        void RememberNormalBounds()
        {
            if (window.WindowState == WindowState.Normal && window.IsVisible)
            {
                double scaling = window.DesktopScaling;
                normalBounds = new PixelRect(window.Position,
                    new PixelSize((int)Math.Round(window.ClientSize.Width * scaling), (int)Math.Round(window.ClientSize.Height * scaling)));
            }
        }
    }

    private static void Apply(Window window, WindowPlacement placement)
    {
        double toDips = StandardDpi / Math.Max(1, placement.DeviceDpi);
        window.Width = placement.Width * toDips;
        window.Height = placement.Height * toDips;

        PixelRect saved = new(placement.X, placement.Y, placement.Width, placement.Height);
        if (window.Screens.All.Any(screen => IsVisibleEnough(screen.WorkingArea, saved)))
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Position = new PixelPoint(placement.X, placement.Y);
        }

        if (placement.Maximized)
        {
            window.WindowState = WindowState.Maximized;
        }
    }

    private static bool IsVisibleEnough(PixelRect screen, PixelRect window)
    {
        PixelRect overlap = screen.Intersect(window);
        return overlap.Width >= Math.Min(MinimumVisibleWidth, window.Width) && overlap.Height >= Math.Min(MinimumVisibleHeight, window.Height);
    }
}
