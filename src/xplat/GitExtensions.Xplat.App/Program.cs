using Avalonia;
using GitCommands;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Platform;

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
    ///  True when the command line asks for upstream's clone dialog (<see cref="CloneArguments"/>), which then opens at startup
    ///  with <see cref="CloneUrl"/>.
    /// </summary>
    public static bool StartsWithClone { get; private set; }

    /// <summary>
    ///  The URL the clone dialog starts with.
    /// </summary>
    public static string? CloneUrl { get; private set; }

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

    /// <summary>
    ///  Upstream's command line verb other than <c>fileeditor</c> and <c>clone</c>, or a request for its help: the app then
    ///  shows only that verb's window (<see cref="CommandLineVerbs"/>).
    /// </summary>
    public static CommandLineRequest? Verb { get; private set; }

    /// <summary>
    ///  Upstream's <c>browse</c> or <c>openrepo</c> verb, whose filters and commit the browse window applies once it opened
    ///  <see cref="InitialRepository"/>.
    /// </summary>
    public static CommandLineRequest? BrowseRequest { get; private set; }

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
            StartsWithClone = CloneArguments.AsksForClone(args);
            CloneUrl = CloneArguments.Url(args);
            if (!StartsWithClone && CommandLine.Parse(args) is { } request)
            {
                if (CommandLineVerbs.OpensBrowseWindow(request, Environment.CurrentDirectory, out string? repository))
                {
                    InitialRepository = repository;
                    BrowseRequest = request;
                }
                else
                {
                    Verb = request;
                }
            }

            // Not for the prompt and editor runs above: git started those, and they start nothing themselves.
            LoginShellPath.Apply();
        }

        // As upstream's Program.Main. Among others it fills AppSettings.AvailableEncodings, without which the viewer and the
        // Git config page offer no encoding at all. The prompt starts nothing that needs it, and has to appear at once.
        if (AskPassPrompt is null)
        {
            AppSettings.LoadSettings();
        }

        // Upstream code (the shared core, plugins) shows WinForms message boxes; until now they returned no answer.
        MessageBoxHost.Install();

        // Here, not in App: the tests' headless app must not touch macOS' application object from its own thread, and an
        // exception in a test must fail the test instead of opening upstream's error report.
        return BuildAvaloniaApp()
            .AfterSetup(_ =>
            {
                App.ShowMacOSDockIcon();
                ErrorReporter.Install();
            })
            .StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
