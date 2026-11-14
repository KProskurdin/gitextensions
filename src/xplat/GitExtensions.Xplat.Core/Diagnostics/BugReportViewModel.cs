using BugReporter.Info;
using BugReporter.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitExtensions.Xplat.Core.Presentation;
using DialogResult = System.Windows.Forms.DialogResult;

namespace GitExtensions.Xplat.Core.Diagnostics;

/// <summary>
///  The view model of upstream's <c>BugReportForm</c>, with its <c>ExceptionDetails</c> and <c>ExceptionDetailView</c>: an
///  exception, the environment and the user's description, copied to the clipboard or sent as a new GitHub issue.
///  <see cref="Result"/> is upstream's dialog result: Abort to quit the application, Ignore to go on, Cancel when the
///  window was closed.
/// </summary>
public sealed partial class BugReportViewModel : DialogViewModel
{
    /// <summary>
    ///  The upstream form whose texts the window shows, by their names.
    /// </summary>
    public const string Category = "BugReportForm";

    private readonly SerializableException _exception;
    private readonly string _exceptionInfo;
    private readonly string _environmentInfo;
    private readonly bool _canIgnore;

    /// <param name="exceptionInfo">Upstream's text about the exception, for the report.</param>
    /// <param name="environmentInfo">Upstream's environment information, for the report.</param>
    /// <param name="canIgnore">The error can be ignored: the window can be closed and Ignore used.</param>
    /// <param name="showIgnore">Upstream shows Ignore.</param>
    /// <param name="focusDetails">The user's own command failed: the Exception tab shows and no report is offered.</param>
    public BugReportViewModel(IDialogHost host, SerializableException exception, string exceptionInfo,
        string environmentInfo, bool canIgnore, bool showIgnore, bool focusDetails)
        : base(host)
    {
        _exception = exception;
        _exceptionInfo = exceptionInfo;
        _environmentInfo = environmentInfo;
        _canIgnore = canIgnore;
        GeneralInfo info = ErrorReports.GeneralInfo(exception);
        Title = $"{info.HostApplication} {TitleText}";
        ExceptionType = exception.Type;
        ExceptionMessage = exception.Message;
        TargetSite = exception.TargetSite;
        Application = $"{info.HostApplication} [{info.HostApplicationVersion}]";
        GitVersion = info.GitVersion;
        DateTime = info.DateTime;
        ClrVersion = info.ClrVersion;
        ExceptionNode root = ExceptionDetails.Tree(exception);
        Exceptions = [root];
        SelectedException = root;
        ShowsDetails = focusDetails;

        // As upstream: the user's own command failed, so a report to the team is held back.
        CanSend = !focusDetails;
        ShowIgnore = showIgnore;
        CanIgnore = showIgnore && canIgnore;
        CopyTip = Host.Translate(Category, "_toolTipCopy", "Copy the issue details into clipboard");
        SendTip = Host.Translate(Category, "_toolTipSendQuit",
            "Report the issue to GitHub and quit application.\r\nA valid GitHub account is required");
        QuitTip = Host.Translate(Category, "_toolTipQuit", "Quit application without reporting the issue");
    }

    /// <summary>
    ///  Raised when Send and Quit needs a description first: the window focuses its box.
    /// </summary>
    public event EventHandler? DescriptionRequired;

    public string Title { get; }

    public string? ExceptionType { get; }

    public string? ExceptionMessage { get; }

    public string? TargetSite { get; }

    public string Application { get; }

    public string? GitVersion { get; }

    public string? DateTime { get; }

    public string? ClrVersion { get; }

    /// <summary>
    ///  Upstream's exception tree: the exception and its inner ones.
    /// </summary>
    public IReadOnlyList<ExceptionNode> Exceptions { get; }

    /// <summary>
    ///  The exception whose properties show.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Properties))]
    public partial ExceptionNode? SelectedException { get; set; }

    public IReadOnlyList<ExceptionProperty> Properties
        => ExceptionDetails.Properties(SelectedException?.Exception ?? _exception);

    [ObservableProperty]
    public partial ExceptionProperty? SelectedProperty { get; set; }

    /// <summary>
    ///  What the user wrote about the error.
    /// </summary>
    [ObservableProperty]
    public partial string? Description { get; set; }

    /// <summary>
    ///  The Exception tab shows first.
    /// </summary>
    public bool ShowsDetails { get; }

    public bool CanSend { get; }

    public bool ShowIgnore { get; }

    public bool CanIgnore { get; }

    public string CopyTip { get; }

    public string SendTip { get; }

    public string QuitTip { get; }

    /// <summary>
    ///  How the window was closed, as upstream's dialog result.
    /// </summary>
    public DialogResult Result { get; private set; } = DialogResult.Cancel;

    /// <summary>
    ///  Upstream hides the window's close box when the error cannot be ignored (ControlBox), so Quit or Send and Quit
    ///  are the only ways out.
    /// </summary>
    public bool MayClose => _canIgnore || Result != DialogResult.Cancel;

    /// <summary>
    ///  Opens the new issue's address; the window sets the browser, tests replace it.
    /// </summary>
    public Action<string>? OpenUrl { get; set; }

    /// <summary>
    ///  Copies a text to the clipboard; the window sets it, as the clipboard is the platform's.
    /// </summary>
    public Func<string, Task>? CopyText { get; set; }

    /// <summary>
    ///  Shows a property's whole value (upstream's <c>ExceptionDetailView</c>); the window sets it, as it opens a window.
    /// </summary>
    public Action<string, string>? ShowPropertyValue { get; set; }

    private string TitleText => Host.Translate(Category, "_title", "Error Report");

    private string SubmitGitHubMessage => Host.Translate(Category, "_submitGitHubMessage",
        """
        Give as much as information as possible please to help the developers solve this issue. Otherwise, your issue ticket may be closed without any follow-up from the developers.

        Because of this, make sure to fill in all the fields in the report template please.

        Send report?
        """);

    private string NoReproStepsSupplied => Host.Translate(Category, "_noReproStepsSuppliedErrorMessage",
        "Please provide as much as information as possible to help the developers solve this issue.");

    [RelayCommand]
    private Task CopyAsync() => GuardAsync(async () =>
    {
        string report = ErrorReports.CopyText(_exception, _exceptionInfo, _environmentInfo, Description ?? "");
        if (!string.IsNullOrWhiteSpace(report) && CopyText is not null)
        {
            await CopyText(report);
        }
    });

    [RelayCommand]
    private void Ignore() => CloseWith(DialogResult.Ignore);

    [RelayCommand]
    private void Quit() => CloseWith(DialogResult.Abort);

    [RelayCommand]
    private Task SendAndQuitAsync() => GuardAsync(async () =>
    {
        string description = Description ?? "";
        if (!ErrorReports.ContainsInfo(description))
        {
            await Host.ShowMessageAsync(NoReproStepsSupplied, TitleText);
            DescriptionRequired?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (!await Host.ConfirmAsync(SubmitGitHubMessage, TitleText, defaultIsNo: true))
        {
            return;
        }

        if (ErrorReports.NewIssueAddress(_exception, _exceptionInfo, _environmentInfo, description) is { } address)
        {
            OpenUrl?.Invoke(address);
        }

        CloseWith(DialogResult.Abort);
    });

    // Upstream's ExceptionDetailView: the whole value of a row, which the list cuts to one line.
    [RelayCommand]
    private void ShowProperty()
    {
        if (SelectedProperty is { } row)
        {
            ShowPropertyValue?.Invoke(row.Property, row.Information);
        }
    }

    private void CloseWith(DialogResult result)
    {
        Result = result;
        Host.Close(null);
    }
}
