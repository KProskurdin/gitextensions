using Avalonia;
using GitExtensions.Xplat.Core.Operations;

namespace GitExtensions.Xplat.App;

internal static class Program
{
    // Upstream's exit code when a command fails or is cancelled (see upstream Program.RunApplication).
    private const int FailedExitCode = -1;

    /// <summary>
    ///  Repository to open at startup, given as the first command-line argument.
    /// </summary>
    public static string? InitialRepository { get; private set; }

    /// <summary>
    ///  The file given with upstream's <c>fileeditor</c> verb. The app then shows only the editor window, as git expects of
    ///  the editor it starts for a rebase todo list or a commit message.
    /// </summary>
    public static string? FileToEdit { get; private set; }

    /// <summary>
    ///  The question git or ssh asks when it started the app as its askpass program. The app then shows only the prompt
    ///  window and prints the answer.
    /// </summary>
    public static string? AskPassPrompt { get; private set; }

    [STAThread]
    public static int Main(string[] args)
    {
        if (GitAskPass.IsAskPassRun(Environment.GetEnvironmentVariable(GitAskPass.MarkerVariable), args))
        {
            AskPassPrompt = args[0];
        }
        else if (args is [GitEditorCommand.Verb, ..])
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Cannot open file editor, there is no file selected.");
                return FailedExitCode;
            }

            FileToEdit = args[1];
        }
        else
        {
            InitialRepository = args.Length > 0 ? args[0] : null;
        }

        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
