using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace GitExtensions.Xplat.App;

/// <summary>
///  The developer aid behind <c>XPLAT_SCREENSHOT=&lt;png&gt;</c>: a window saves its content to the file and closes, so a
///  script or CI can check a run without a person at the screen.
/// </summary>
internal static class Screenshot
{
    public const string EnvironmentVariable = "XPLAT_SCREENSHOT";

    /// <summary>
    ///  The file to save to, or null when the aid is off.
    /// </summary>
    public static string? RequestedFile
        => Environment.GetEnvironmentVariable(EnvironmentVariable) is { Length: > 0 } file ? file : null;

    public static async Task SaveAsync(Window window, string file)
    {
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
        using RenderTargetBitmap bitmap = new(new PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height));
        bitmap.Render(window);
        await using FileStream stream = File.Create(file);
        bitmap.Save(stream);
    }
}
