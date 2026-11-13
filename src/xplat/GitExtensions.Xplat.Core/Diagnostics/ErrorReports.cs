using System.ComponentModel;
using System.Security;
using System.Text;
using BugReporter;
using BugReporter.Info;
using BugReporter.Serialization;
using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Settings;
using GitExtensions.Xplat.Core.Operations;

namespace GitExtensions.Xplat.Core.Diagnostics;

/// <summary>
///  What upstream's <c>BugReportInvoker.Report</c> shows for an exception.
/// </summary>
public enum ErrorReportKind
{
    /// <summary>
    ///  Nothing: a cancellation.
    /// </summary>
    None,

    /// <summary>
    ///  A file or assembly that could not be loaded, which a restart usually fixes.
    /// </summary>
    FailedToLoadAnAssembly,

    /// <summary>
    ///  git refused the repository, as another user owns it; the report offers to trust it.
    /// </summary>
    DubiousOwnership,

    /// <summary>
    ///  Any other error, with the details and the choice to report it.
    /// </summary>
    Error,
}

/// <summary>
///  How upstream's <c>BugReportInvoker.Report</c> sorts an exception, and what its dialog shows.
/// </summary>
/// <param name="Kind">The dialog to show.</param>
/// <param name="RootError">The innermost message (upstream's <c>GetRootError</c>), the dialog's heading.</param>
/// <param name="IsExternalOperation">The error came from git or another program, a file or the OS, so upstream offers
///  Ignore first and the report second.</param>
/// <param name="IsUserExternalOperation">The error came from git or a program the user configured: upstream offers the
///  details, not a bug report.</param>
public sealed record ErrorReport(
    ErrorReportKind Kind,
    string RootError,
    bool IsExternalOperation,
    bool IsUserExternalOperation);

/// <summary>
///  The texts upstream's <c>GetExceptionInfo</c> puts before each detail of a failed command (upstream's
///  <c>TranslatedStrings</c>).
/// </summary>
public sealed record ExceptionInfoLabels(
    string ExitCode = "Exit code",
    string Command = "Command",
    string Arguments = "Arguments",
    string WorkingDirectory = "Working directory");

/// <summary>
///  What git said when it refused a repository it does not trust (upstream's <c>CreateDubiousOwnershipReport</c>).
/// </summary>
/// <param name="TrustCommand">git's own suggestion, <c>config --global --add safe.directory ...</c>, without "git".</param>
/// <param name="Folder">The repository's folder.</param>
/// <param name="GitOutput">git's whole message.</param>
public sealed record DubiousOwnership(string TrustCommand, string Folder, string GitOutput);

/// <summary>
///  Upstream's error reporting without its WinForms dialogs: <c>BugReportInvoker</c> (which exceptions are shown and how,
///  the error log), <c>ExceptionExtensions.GetExceptionInfo</c>, and <c>UIReporter</c>'s rules for files that failed to
///  load and for repositories git does not trust. The windows are the app's.
/// </summary>
public static class ErrorReports
{
    /// <summary>
    ///  The part of git's message that names the command to trust a repository (upstream's
    ///  <c>DubiousOwnershipSecurityConfigString</c>).
    /// </summary>
    public const string DubiousOwnershipSecurityConfigString = "config --global --add safe.directory";

    /// <summary>
    ///  Where upstream's "Send and Quit" opens a new issue.
    /// </summary>
    public const string NewIssueUrl = "https://github.com/gitextensions/gitextensions/issues/new";

    // As upstream's UIReporter.HasFailedToLoadAnAssembly.
    private const string CouldNotLoadFileOrAssembly = "Could not load file or assembly";
    private const string VersionPart = ", version=";
    private const string VcRuntime = "vcruntime";
    private const string UnknownFile = "unknown";

    private static readonly ErrorReportUrlBuilder _bodyBuilder = new();
    private static readonly GitHubUrlBuilder _urlBuilder = new(_bodyBuilder);

    /// <summary>
    ///  Upstream's <c>BugReportInvoker.Report</c>: which dialog <paramref name="exception"/> gets. The error log is
    ///  <see cref="LogError"/>, written first when <see cref="AppSettings.WriteErrorLog"/> is on.
    /// </summary>
    public static ErrorReport Classify(Exception exception)
    {
        string rootError = RootError(exception);
        if (HasFailedToLoadAnAssembly(exception))
        {
            return new ErrorReport(ErrorReportKind.FailedToLoadAnAssembly, rootError, false, false);
        }

        // Upstream does not report the cancellation of work the UI thread awaited (upstream issue 11636).
        if (exception is OperationCanceledException)
        {
            return new ErrorReport(ErrorReportKind.None, rootError, false, false);
        }

        if (FindDubiousOwnership(exception) is not null)
        {
            return new ErrorReport(ErrorReportKind.DubiousOwnership, rootError, true, true);
        }

        ExternalOperationException? external = exception as ExternalOperationException;

        // Upstream treats every git error as the user's; the new shell's own git runners raise GitOperationException.
        bool isUserExternalOperation = exception is UserExternalOperationException or GitConfigFormatException
                                           or GitOperationException
                                       || string.Equals(AppSettings.GitCommand, external?.Command,
                                           StringComparison.InvariantCultureIgnoreCase)
                                       || string.Equals(AppSettings.WslCommand, external?.Command,
                                           StringComparison.InvariantCultureIgnoreCase);
        bool isExternalOperation = isUserExternalOperation
                                   || exception is ExternalOperationException or IOException or SecurityException
                                       or Win32Exception;
        return new ErrorReport(ErrorReportKind.Error, rootError, isExternalOperation, isUserExternalOperation);
    }

    /// <summary>
    ///  Upstream's <c>GetRootError</c>: the innermost exception's message that is not empty.
    /// </summary>
    public static string RootError(Exception exception)
    {
        string rootError = exception.Message;
        for (Exception? inner = exception.InnerException; inner is not null; inner = inner.InnerException)
        {
            if (!string.IsNullOrEmpty(inner.Message))
            {
                rootError = inner.Message;
            }
        }

        return rootError;
    }

    /// <summary>
    ///  Upstream's <c>GetExceptionInfo</c>: for a failed command its context, exit code, command, arguments and folder, one
    ///  per line; empty for other exceptions.
    /// </summary>
    public static string ExceptionInfo(Exception exception, ExceptionInfoLabels labels)
    {
        StringBuilder text = new();
        if (exception is UserExternalOperationException user && !string.IsNullOrWhiteSpace(user.Context))
        {
            text.AppendLine(user.Context);
        }

        if (exception is ExternalOperationException external)
        {
            if (external.ExitCode is int exitCode)
            {
                AppendIfNotEmpty(ExecutionResult.FormatExitCode(exitCode), labels.ExitCode);
            }

            AppendIfNotEmpty(external.Command, labels.Command);
            AppendIfNotEmpty(external.Arguments, labels.Arguments);
            AppendIfNotEmpty(external.WorkingDirectory, labels.WorkingDirectory);
        }

        return text.ToString();

        void AppendIfNotEmpty(string? value, string designation)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                text.Append(designation).Append(": ").AppendLine(value);
            }
        }
    }

    /// <summary>
    ///  Upstream's <c>LogError</c>: writes the exception to a file in the temporary folder, named after the application,
    ///  its version and the time. Returns the file, or null with <paramref name="error"/> when it could not be written.
    /// </summary>
    public static string? LogError(Exception exception, bool isTerminating, out string? error)
    {
        string file = Path.Join(Path.GetTempPath(),
            $"{AppSettings.ApplicationId}.{AppSettings.AppVersion}.{DateTime.Now:yyyyMMdd.HHmmssfff}.log");
        try
        {
            File.WriteAllText(file, $"Is fatal: {isTerminating}\r\n{exception}");
            error = null;
            return file;
        }
        catch (Exception ex)
        {
            error = $"Failed to log error to {file}\r\n{ex.Message}";
            return null;
        }
    }

    /// <summary>
    ///  git's refusal of an untrusted repository in <paramref name="exception"/> or its inner exceptions, or null.
    /// </summary>
    public static DubiousOwnership? FindDubiousOwnership(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            string? folder = (current as ExternalOperationException)?.WorkingDirectory
                             ?? (exception as ExternalOperationException)?.WorkingDirectory;
            if (DubiousOwnershipIn(current.Message, folder) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>
    ///  git's refusal of an untrusted repository in <paramref name="gitOutput"/>, or null. Upstream takes the folder from
    ///  the failed command, else from git's suggested command.
    /// </summary>
    public static DubiousOwnership? DubiousOwnershipIn(string? gitOutput, string? workingDirectory = null)
    {
        int start = gitOutput?.IndexOf(DubiousOwnershipSecurityConfigString, StringComparison.Ordinal) ?? -1;
        if (start < 0)
        {
            return null;
        }

        // git puts its suggestion on a line of its own; anything after it (other errors) is not part of the command.
        string suggestion = gitOutput![start..].Split('\n')[0].Trim();
        string command = ReplaceRepoPathQuotes(suggestion);
        string folder = workingDirectory
                        ?? Unquote(suggestion[
                            Math.Min(suggestion.Length, DubiousOwnershipSecurityConfigString.Length + 1)..].Trim());
        return new DubiousOwnership(command, folder, gitOutput.Trim());
    }

    /// <summary>
    ///  Runs git's suggested command to trust the repository, in <paramref name="workingDirectory"/>, as upstream's "Trust
    ///  this repository" does.
    /// </summary>
    public static Task<ExecutionResult> TrustRepositoryAsync(DubiousOwnership dubious, string workingDirectory)
        => Task.Run(() => new GitExecutorProvider(new GitDirectoryResolver()).GetExecutor(workingDirectory)
            .GitExecutable
            .Execute(dubious.TrustCommand, throwOnErrorExit: false));

    /// <summary>
    ///  Upstream's <c>UIReporter.GetFileName</c>: the file that failed to load, without its version, or "unknown".
    /// </summary>
    public static string FailedFileName(Exception exception)
    {
        if (exception is FileNotFoundException notFound)
        {
            string name = notFound.FileName ?? "";
            int uninteresting = name.IndexOf(VersionPart, StringComparison.InvariantCultureIgnoreCase);
            return uninteresting > 0 ? name[..uninteresting] : name;
        }

        if (exception is DllNotFoundException dllNotFound)
        {
            // From a message such as "Unable to load DLL 'vcruntime140_cor3.dll'".
            string name = dllNotFound.Message;
            int start = name.IndexOf('\'');
            int end = start >= 0 ? name.IndexOf('\'', start + 1) : -1;
            return end > start ? name[(start + 1)..end] : name;
        }

        return UnknownFile;
    }

    /// <summary>
    ///  Upstream offers to report a file that failed to load only when it is neither .NET's (whose failures upstream puts
    ///  down to updates) nor the VC runtime's.
    /// </summary>
    public static bool CanReportFailedFile(string fileName)
        => !IsDotNetFrameworkAssembly(fileName) && !IsVcRuntimeDll(fileName);

    /// <summary>
    ///  Upstream's report form's General tab: application, git and .NET versions and the time, from
    ///  <paramref name="exception"/>.
    /// </summary>
    public static GeneralInfo GeneralInfo(SerializableException exception) => new(exception);

    /// <summary>
    ///  The address of a new GitHub issue filled with the report, as upstream's "Send and Quit" opens it.
    /// </summary>
    public static string? NewIssueAddress(SerializableException exception, string exceptionInfo, string environmentInfo,
        string description)
        => _urlBuilder.Build(NewIssueUrl, exception, exceptionInfo, environmentInfo, description);

    /// <summary>
    ///  The report as upstream's Copy puts it on the clipboard.
    /// </summary>
    public static string CopyText(SerializableException exception, string exceptionInfo, string environmentInfo,
        string description)
        => _bodyBuilder.CopyText(exception, exceptionInfo, environmentInfo, description);

    /// <summary>
    ///  Upstream's <c>CheckContainsInfo</c>: the description has something besides white space.
    /// </summary>
    public static bool ContainsInfo(string? description) => !string.IsNullOrWhiteSpace(description);

    private static bool HasFailedToLoadAnAssembly(Exception exception)
        => (exception is FileNotFoundException notFound
            && notFound.Message.StartsWith(CouldNotLoadFileOrAssembly, StringComparison.Ordinal))
           || (exception is DllNotFoundException dllNotFound && IsVcRuntimeDll(dllNotFound.Message))
           || (exception.InnerException is { } inner && HasFailedToLoadAnAssembly(inner));

    private static bool IsDotNetFrameworkAssembly(string name)
        => !string.IsNullOrWhiteSpace(name)
           && (name.StartsWith("System.", StringComparison.OrdinalIgnoreCase)
               || name.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase));

    private static bool IsVcRuntimeDll(string name)
        => !string.IsNullOrWhiteSpace(name) && name.Contains(VcRuntime, StringComparison.OrdinalIgnoreCase);

    // Upstream's ReplaceRepoPathQuotes: on Windows, single quotes around the path become double quotes.
    private static string ReplaceRepoPathQuotes(string command)
    {
        if (!OperatingSystem.IsWindows() || !command.EndsWith('\''))
        {
            return command;
        }

        int quote = command.IndexOf('\'');
        return quote < 0 ? command : $"{command[..quote]}\"{command[(quote + 1)..^1]}\"";
    }

    private static string Unquote(string path)
        => path.Length >= 2 && path[0] == path[^1] && path[0] is '\'' or '"' ? path[1..^1] : path;
}
