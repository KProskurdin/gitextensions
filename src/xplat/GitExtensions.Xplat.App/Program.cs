using Avalonia;

namespace GitExtensions.Xplat.App;

internal static class Program
{
    /// <summary>
    ///  Repository to open at startup, given as the first command-line argument.
    /// </summary>
    public static string? InitialRepository { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        InitialRepository = args.Length > 0 ? args[0] : null;
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
