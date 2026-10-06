using Avalonia;
using Avalonia.Controls;
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
        ThemeApplier.Apply(AppServices.Preferences);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // The prompt and editor windows decide the exit code git reads, so closing them must not end the app with 0
            // before their own shutdown runs.
            if (Program.AskPassPrompt is not null || Program.FileToEdit is not null)
            {
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            }

            if (Program.AskPassPrompt is { } prompt)
            {
                AskPassWindow askPass = new(prompt);
                askPass.Closed += (_, _) =>
                {
                    // git and ssh read one line; a cancelled prompt exits with an error, which stops the operation.
                    if (askPass.Answer is { } answer)
                    {
                        Console.Out.WriteLine(answer);
                        Console.Out.Flush();
                    }

                    desktop.Shutdown(askPass.Answer is null ? 1 : 0);
                };
                desktop.MainWindow = askPass;
            }
            else if (Program.FileToEdit is { } file)
            {
                EditorWindow editor = new(file);
                editor.Closed += (_, _) => desktop.Shutdown(editor.Accepted ? 0 : -1);
                desktop.MainWindow = editor;
            }
            else
            {
                desktop.MainWindow = new MainWindow(FindGit());
            }
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
