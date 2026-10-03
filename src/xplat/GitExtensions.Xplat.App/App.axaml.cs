using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using GitCommands;

namespace GitExtensions.Xplat.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // The shared core marshals work onto the thread that created this context, i.e. the UI thread.
        GitUI.ThreadHelper.JoinableTaskContext = new Microsoft.VisualStudio.Threading.JoinableTaskContext();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow(FindGit());
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static GitDiscoveryResult? FindGit()
    {
        if (OperatingSystem.IsWindows())
        {
            return null;
        }

        GitDiscoveryResult result = XplatGitDiscovery.Discover(AppSettings.GitCommandValue);
        if (result.Status == GitDiscoveryStatus.Found && result.Command != AppSettings.GitCommandValue)
        {
            AppSettings.GitCommandValue = result.Command!;
        }

        return result;
    }
}
