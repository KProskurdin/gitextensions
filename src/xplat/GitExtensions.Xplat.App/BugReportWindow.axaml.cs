using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using BugReporter.Info;
using BugReporter.Serialization;
using GitCommands;
using GitExtensions.Xplat.Core.Diagnostics;
using GitExtensions.Xplat.Ui;
using DialogResult = System.Windows.Forms.DialogResult;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;
using MessageBoxDefaultButton = System.Windows.Forms.MessageBoxDefaultButton;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>BugReportForm</c>, with its <c>ExceptionDetails</c> and <c>ExceptionDetailView</c>: an exception, the
///  environment and the user's description, copied to the clipboard or sent as a new GitHub issue. <see cref="Result"/>
///  is upstream's dialog result: Abort to quit the application, Ignore to go on, Cancel when the window was closed.
/// </summary>
public partial class BugReportWindow : Window
{
    // Upstream translates the form by its name.
    private const string Category = "BugReportForm";

    private readonly SerializableException _exception;
    private readonly string _exceptionInfo;
    private readonly string _environmentInfo;
    private readonly bool _canIgnore;

    /// <summary>
    ///  The design-time constructor (<see cref="DesignTime"/>): the window's XAML only.
    /// </summary>
    [Obsolete(DesignTime.ConstructorOnly)]
#pragma warning disable CS8618 // The fields the window's data fills stay unset: no code of the window runs.
    public BugReportWindow() => InitializeComponent();
#pragma warning restore CS8618

    public BugReportWindow(SerializableException exception, string exceptionInfo, string environmentInfo, bool canIgnore,
        bool showIgnore, bool focusDetails)
    {
        InitializeComponent();
        UpstreamTranslation.Apply(this, Category);
        _exception = exception;
        _exceptionInfo = exceptionInfo;
        _environmentInfo = environmentInfo;
        _canIgnore = canIgnore;

        GeneralInfo info = ErrorReports.GeneralInfo(exception);
        Title = $"{info.HostApplication} {TitleText}";
        exceptionTextBox.Text = exception.Type;
        exceptionMessageTextBox.Text = exception.Message;
        targetSiteTextBox.Text = exception.TargetSite;
        applicationTextBox.Text = $"{info.HostApplication} [{info.HostApplicationVersion}]";
        gitTextBox.Text = info.GitVersion;
        dateTimeTextBox.Text = info.DateTime;
        clrTextBox.Text = info.ClrVersion;

        ExceptionNode root = ExceptionDetails.Tree(exception);
        exceptionTreeView.ItemsSource = new[] { root };
        exceptionTreeView.SelectionChanged += (_, _) =>
        {
            if (exceptionTreeView.SelectedItem is ExceptionNode node)
            {
                exceptionDetailsListView.ItemsSource = ExceptionDetails.Properties(node.Exception);
            }
        };
        exceptionTreeView.SelectedItem = root;
        exceptionDetailsListView.ItemsSource = ExceptionDetails.Properties(exception);
        exceptionDetailsListView.DoubleTapped += (_, _) => ShowSelectedProperty();

        if (focusDetails)
        {
            mainTabs.SelectedItem = exceptionTabPage;

            // As upstream: the user's own command failed, so a report to the team is held back.
            sendAndQuitButton.IsEnabled = false;
        }

        IgnoreButton.IsEnabled = showIgnore && canIgnore;
        IgnoreButton.IsVisible = showIgnore;

        ToolTip.SetTip(btnCopy, UpstreamTranslation.Text(Category, "_toolTipCopy", "Copy the issue details into clipboard"));
        ToolTip.SetTip(sendAndQuitButton, UpstreamTranslation.Text(Category, "_toolTipSendQuit",
            "Report the issue to GitHub and quit application.\r\nA valid GitHub account is required"));
        ToolTip.SetTip(quitButton,
            UpstreamTranslation.Text(Category, "_toolTipQuit", "Quit application without reporting the issue"));

        btnCopy.Click += (_, _) => UiActions.Run(CopyAsync, ShowError);
        IgnoreButton.Click += (_, _) => CloseWith(DialogResult.Ignore);
        quitButton.Click += (_, _) => CloseWith(DialogResult.Abort);
        sendAndQuitButton.Click += (_, _) => UiActions.Run(SendAndQuitAsync, ShowError);

        // Upstream hides the window's close box when the error cannot be ignored (ControlBox), so Quit or Send and Quit
        // are the only ways out.
        Closing += (_, e) => e.Cancel |= !_canIgnore && Result == DialogResult.Cancel;
        Opened += (_, _) => descriptionTextBox.Focus();
    }

    /// <summary>
    ///  How the window was closed, as upstream's dialog result.
    /// </summary>
    public DialogResult Result { get; private set; } = DialogResult.Cancel;

    /// <summary>
    ///  What the user wrote about the error.
    /// </summary>
    public string Description => descriptionTextBox.Text ?? "";

    /// <summary>
    ///  Opens the new issue's address; tests replace it.
    /// </summary>
    public Action<string> OpenUrl { get; set; } = OsShellUtil.OpenUrlInDefaultBrowser;

    private static string TitleText => UpstreamTranslation.Text(Category, "_title", "Error Report");

    private static string SubmitGitHubMessage => UpstreamTranslation.Text(Category, "_submitGitHubMessage",
        """
        Give as much as information as possible please to help the developers solve this issue. Otherwise, your issue ticket may be closed without any follow-up from the developers.

        Because of this, make sure to fill in all the fields in the report template please.

        Send report?
        """);

    private static string NoReproStepsSupplied => UpstreamTranslation.Text(Category, "_noReproStepsSuppliedErrorMessage",
        "Please provide as much as information as possible to help the developers solve this issue.");

    private void CloseWith(DialogResult result)
    {
        Result = result;
        Close();
    }

    private async Task CopyAsync()
    {
        string report = ErrorReports.CopyText(_exception, _exceptionInfo, _environmentInfo, Description);
        if (!string.IsNullOrWhiteSpace(report) && Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(report);
        }
    }

    private async Task SendAndQuitAsync()
    {
        if (!ErrorReports.ContainsInfo(Description))
        {
            await new MessageBoxWindow(NoReproStepsSupplied, TitleText, MessageBoxButtons.OK).ShowDialog(this);
            descriptionTextBox.Focus();
            return;
        }

        MessageBoxWindow confirm = new(SubmitGitHubMessage, TitleText, MessageBoxButtons.YesNo,
            MessageBoxDefaultButton.Button2);
        await confirm.ShowDialog(this);
        if (confirm.Result != DialogResult.Yes)
        {
            return;
        }

        if (ErrorReports.NewIssueAddress(_exception, _exceptionInfo, _environmentInfo, Description) is { } address)
        {
            OpenUrl(address);
        }

        CloseWith(DialogResult.Abort);
    }

    // Upstream's ExceptionDetailView: the whole value of a row, which the list cuts to one line.
    private void ShowSelectedProperty()
    {
        if (exceptionDetailsListView.SelectedItem is ExceptionProperty row)
        {
            _ = new ExceptionDetailWindow(row.Property, row.Information).ShowDialog(this);
        }
    }

    private void ShowError(Exception ex) => _ = new ErrorWindow(ex.Message).ShowDialog(this);
}
