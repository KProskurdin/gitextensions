using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using BugReporter.Serialization;
using GitCommands;
using GitExtensions.Xplat.Core.Diagnostics;
using GitExtensions.Xplat.Core.Platform;
using GitExtensions.Xplat.Ui;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;
using TaskDialogButton = System.Windows.Forms.TaskDialogButton;
using TaskDialogCommandLinkButton = System.Windows.Forms.TaskDialogCommandLinkButton;
using TaskDialogIcon = System.Windows.Forms.TaskDialogIcon;
using TaskDialogPage = System.Windows.Forms.TaskDialogPage;
using WinFormsDialogResult = System.Windows.Forms.DialogResult;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>BugReportInvoker</c> and <c>UIReporter</c> for the new shell: shows an exception nothing else handled, as
///  upstream's <c>Program.Main</c> has every UI-thread and every terminating exception shown. The rules are
///  <see cref="ErrorReports"/>; the dialogs are upstream's task dialogs (<see cref="MessageBoxWindow"/>) and
///  <see cref="BugReportWindow"/>.
/// </summary>
public static class ErrorReporter
{
    private const string TranslatedStrings = "TranslatedStrings";

    // Upstream's link for safe.directory.
    private const string SafeDirectoryHelpUrl =
        "https://git-scm.com/docs/git-config/#Documentation/git-config.txt-safedirectory";

    // Upstream's exit code after Quit or Send and Quit.
    private const int QuitExitCode = -1;

    private static bool _reporting;
    private static bool _reportingDubiousOwnership;
    private static bool _ignoreFailedToLoadAnAssembly;

    /// <summary>
    ///  Ends the application after Quit; tests replace it.
    /// </summary>
    public static Action<int> Exit { get; set; } = Environment.Exit;

    /// <summary>
    ///  Opens a web address (the safe.directory help); tests replace it.
    /// </summary>
    public static Action<string> OpenUrl { get; set; } = OsShellUtil.OpenUrlInDefaultBrowser;

    /// <summary>
    ///  Raised with each window of a report before it is shown; tests use it to answer the window.
    /// </summary>
    public static event EventHandler<Window>? Showing;

    private static ExceptionInfoLabels Labels => new(
        UpstreamTranslation.Text(TranslatedStrings, "_exitCodeText", "Exit code"),
        UpstreamTranslation.Text(TranslatedStrings, "_commandText", "Command"),
        UpstreamTranslation.Text(TranslatedStrings, "_argumentsText", "Arguments"),
        UpstreamTranslation.Text(TranslatedStrings, "_workingDirectoryText", "Working directory"));

    /// <summary>
    ///  As upstream's <c>Program.Main</c>: every exception that escapes the UI thread is shown and the app goes on, and an
    ///  exception that ends the process is shown before it does. Not for the headless tests, where an exception must fail
    ///  the test.
    /// </summary>
    public static void Install()
    {
        Dispatcher.UIThread.UnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
    }

    /// <summary>
    ///  The handler <see cref="Install"/> adds to the UI thread's dispatcher.
    /// </summary>
    public static void OnDispatcherUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        _ = ReportAsync(e.Exception, isTerminating: false);
    }

    /// <summary>
    ///  Upstream's <c>BugReportInvoker.Report</c>: writes the error log when the setting asks for it, then shows the dialog
    ///  that suits <paramref name="exception"/>. One report is shown at a time; exceptions raised while it is open are
    ///  traced only, so an error repeated on every render does not stack windows.
    /// </summary>
    public static async Task ReportAsync(Exception exception, bool isTerminating)
    {
        if (AppSettings.WriteErrorLog && ErrorReports.LogError(exception, isTerminating, out string? logError) is null)
        {
            await ShowAsync(new MessageBoxWindow(logError ?? "", "Error writing log", MessageBoxButtons.OK));
        }

        ErrorReport report = ErrorReports.Classify(exception);
        if (report.Kind == ErrorReportKind.None || _reporting)
        {
            Trace.WriteLine(exception);
            return;
        }

        _reporting = true;
        try
        {
            switch (report.Kind)
            {
                case ErrorReportKind.FailedToLoadAnAssembly:
                    await ReportFailedToLoadAnAssemblyAsync(exception, isTerminating);
                    break;
                case ErrorReportKind.DubiousOwnership when ErrorReports.FindDubiousOwnership(exception) is { } dubious:
                    if (await ReportDubiousOwnershipAsync(dubious) && MainWindow() is { } main)
                    {
                        main.SetWorkingDir(dubious.Folder);
                    }

                    break;
                case ErrorReportKind.Error when isTerminating:
                    await ShowBugReportAsync(exception, isExternalOperation: false, isUserExternalOperation: false,
                        isTerminating: true);
                    break;
                default:
                    await ReportErrorAsync(exception, report);
                    break;
            }
        }
        finally
        {
            _reporting = false;
        }
    }

    /// <summary>
    ///  Upstream's dubious ownership report: git refused the repository, and the user can trust it, open its folder or read
    ///  git's help. Returns true when the repository was trusted, and the caller opens it again. Shown once at a time, as
    ///  every read of the repository fails the same way.
    /// </summary>
    public static async Task<bool> ReportDubiousOwnershipAsync(DubiousOwnership dubious)
    {
        if (_reportingDubiousOwnership)
        {
            return false;
        }

        _reportingDubiousOwnership = true;
        try
        {
            while (true)
            {
                TaskDialogCommandLinkButton openFolder = new(Text("_gitDubiousOwnershipOpenRepositoryFolder",
                    "Open repository in Explorer"));
                TaskDialogCommandLinkButton trust = new(
                    Text("_gitDubiousOwnershipTrustRepository", "Trust this repository"),
                    $"git {dubious.TrustCommand}");
                TaskDialogPage page = new()
                {
                    Icon = TaskDialogIcon.Error,
                    Caption = Text("_gitSecurityError", "Git security error"),
                    Heading = Text("_gitDubiousOwnershipHeader",
                        "Do you want to trust this repository by adding a security exception?"),

                    // Upstream shows git's output in an expander under the text.
                    Text = $"{Text("_gitDubiousOwnershipText", DubiousOwnershipText)}\n\n{dubious.GitOutput}",
                    AllowCancel = true,
                    SizeToContent = true,
                    Buttons = { openFolder, trust, TaskDialogButton.Help, TaskDialogButton.Close },
                };

                TaskDialogButton chosen = await ShowPageAsync(page);
                if (chosen == openFolder)
                {
                    // Upstream's button keeps the dialog open; here the dialog shows again.
                    AppServices.FileManager.OpenFolder(dubious.Folder);
                    continue;
                }

                if (chosen == trust)
                {
                    await ErrorReports.TrustRepositoryAsync(dubious, dubious.Folder);
                    return true;
                }

                if (chosen == TaskDialogButton.Help)
                {
                    OpenUrl(SafeDirectoryHelpUrl);
                }

                return false;
            }
        }
        finally
        {
            _reportingDubiousOwnership = false;
        }
    }

    private static string DubiousOwnershipText => """
                                                  Git detected a security problem that prevents opening the repository.
                                                  Git-tracked directories are considered unsafe if they are owned by someone other than the current user.

                                                  To be able to open this repository, you need to either:
                                                  - add a security exception for the repository to make git trust it, or
                                                  - correct the ownership of the repository.
                                                  """;

    private static void OnAppDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is not Exception exception)
        {
            return;
        }

        // The process ends when this returns, so this thread waits for the report the UI thread shows, as upstream's
        // separate BugReporter process outlives the app.
        try
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                Trace.WriteLine(exception);
                return;
            }

            // A worker thread is blocked here, never the UI thread, which shows the report meanwhile.
#pragma warning disable VSTHRD002
            Dispatcher.UIThread.InvokeAsync(() => ReportAsync(exception, e.IsTerminating)).Wait();
#pragma warning restore VSTHRD002
        }
        catch (Exception ex)
        {
            Trace.WriteLine(ex);
        }
    }

    // Upstream's CreateErrorReport: the innermost message as the heading, the failed command's details, and the choice to
    // ignore the error or report it (the details only, for the user's own command).
    private static async Task ReportErrorAsync(Exception exception, ErrorReport report)
    {
        string text = ErrorReports.ExceptionInfo(exception, Labels);
        TaskDialogPage page = new()
        {
            Icon = report.IsExternalOperation ? TaskDialogIcon.Warning : TaskDialogIcon.Error,
            Caption = Text("_error", "Error"),
            Heading = report.RootError,
            SizeToContent = true,
        };

        string ignoreText = Text("_buttonIgnore", "Ignore");
        if (report.IsExternalOperation)
        {
            page.Buttons.Add(new TaskDialogCommandLinkButton(ignoreText,
                Text("_externalErrorDescription", "error from git or from other external application")));
        }
        else
        {
            text += Environment.NewLine + Text("_reportBugText",
                "If you think this was caused by Git Extensions, you can report a bug for the team to investigate.");
        }

        TaskDialogCommandLinkButton reportButton = report switch
        {
            { IsUserExternalOperation: true } => new(Text("_buttonViewDetails", "View details")),
            { IsExternalOperation: true } => new(Text("_reportIssue", "Report the issue"),
                Text("_reportIssueDescription",
                    $"for the team to investigate - if you think this was caused by {AppSettings.ApplicationName}")),
            _ => new(Text("_buttonReportBug", "Report bug!")),
        };
        page.Buttons.Add(reportButton);
        if (!report.IsExternalOperation)
        {
            page.Buttons.Add(new TaskDialogCommandLinkButton(ignoreText));
        }

        page.Text = text.Trim();
        if (await ShowPageAsync(page) == reportButton)
        {
            await ShowBugReportAsync(exception, report.IsExternalOperation, report.IsUserExternalOperation,
                isTerminating: false);
        }
    }

    // Upstream's CreateFailedToLoadAnAssemblyReport: shown once, with a restart and, for a file not of .NET or the VC
    // runtime, a report.
    private static async Task ReportFailedToLoadAnAssemblyAsync(Exception exception, bool isTerminating)
    {
        if (_ignoreFailedToLoadAnAssembly)
        {
            return;
        }

        _ignoreFailedToLoadAnAssembly = true;
        string fileName = ErrorReports.FailedFileName(exception);
        TaskDialogCommandLinkButton restart = new(
            Text("_restartApplication", $"Restart {AppSettings.ApplicationName}"),
            Text("_restartApplicationDescription",
                "Usually restarting the application solves the issue.\r\nSometimes Windows may need to be restarted too."));
        TaskDialogCommandLinkButton reportButton = new(Text("_reportIssue", "Report the issue"),
            Text("_reportReproducedIssueDescription",
                "If you can consistently reproduce the issue, please report it."));
        TaskDialogPage page = new()
        {
            Icon = TaskDialogIcon.Warning,
            Caption = Text("_failedToLoadAnAssembly", "Failed to load an assembly"),
            Heading = string.Format(Text("_failedToLoadFileOrAssemblyFormat", "Could not load file or assembly '{0}'."),
                fileName),

            // Upstream shows the message in an expander under the text.
            Text =
                $"{Text("_failedToLoadFileOrAssemblyText", "Most of the times the error is temporary, likely caused by Windows Update.")}\n\n{exception.Message}",
            SizeToContent = true,
            Buttons = { restart },
        };
        if (ErrorReports.CanReportFailedFile(fileName))
        {
            page.Buttons.Add(reportButton);
        }

        TaskDialogButton chosen = await ShowPageAsync(page);
        if (chosen == restart)
        {
            Restart();
        }
        else if (chosen == reportButton)
        {
            await ShowBugReportAsync(exception, isExternalOperation: false, isUserExternalOperation: false,
                isTerminating);
        }
    }

    // Upstream's ShowNBug: the report form; Quit and Send and Quit end the application, as does any report of an
    // exception that ends the process.
    private static async Task ShowBugReportAsync(Exception exception, bool isExternalOperation,
        bool isUserExternalOperation, bool isTerminating)
    {
        Window? owner = ModalWindow.OwnerWindow(null);
        string environment = owner is not null
            ? AboutWindow.Information(owner, gitVersion: null)
            : EnvironmentInformation.Information(AppSettings.ProductVersion, ThisAssembly.Git.Sha,
                ThisAssembly.Git.IsDirty,
                EnvironmentInformation.CurrentGitVersion(), scale: 1, AppSettings.IsPortable());
        BugReportWindow window = new(new SerializableException(exception),
            ErrorReports.ExceptionInfo(exception, Labels),
            environment, canIgnore: !isTerminating, showIgnore: isExternalOperation,
            focusDetails: isUserExternalOperation);
        await ShowAsync(window);
        if (isTerminating || window.Result == WinFormsDialogResult.Abort)
        {
            Exit(QuitExitCode);
        }
    }

    private static async Task<TaskDialogButton> ShowPageAsync(TaskDialogPage page)
    {
        MessageBoxWindow box = new(page);
        await ShowAsync(box);
        return box.ChosenButton;
    }

    // Over the active window as upstream's OwnerForm, or on its own when no window is open yet.
    private static async Task ShowAsync(Window window)
    {
        Showing?.Invoke(null, window);
        if (ModalWindow.OwnerWindow(null) is { IsVisible: true } owner && owner != window)
        {
            await window.ShowDialog(owner);
            return;
        }

        TaskCompletionSource closed = new();
        window.Closed += (_, _) => closed.TrySetResult();
        window.Show();
        await closed.Task;
    }

    private static MainWindow? MainWindow()
        => (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
            ?.MainWindow as MainWindow;

    // Upstream's RestartGE: the same command line again, then this process ends.
    private static void Restart()
    {
        if (Environment.ProcessPath is not { } path)
        {
            return;
        }

        ProcessStartInfo start = new(path) { WorkingDirectory = Environment.CurrentDirectory };
        foreach (string argument in Environment.GetCommandLineArgs().Skip(1))
        {
            start.ArgumentList.Add(argument);
        }

        Process.Start(start);
        Exit(0);
    }

    private static string Text(string name, string english) =>
        UpstreamTranslation.Text(TranslatedStrings, name, english);
}
