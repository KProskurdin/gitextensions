using Avalonia.Controls;
using Avalonia.Input.Platform;
using BugReporter.Serialization;
using GitCommands;
using GitExtensions.Xplat.Core.Diagnostics;
using GitExtensions.Xplat.Ui;
using DialogResult = System.Windows.Forms.DialogResult;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>BugReportForm</c>, bound to <see cref="BugReportViewModel"/>: an exception, the environment and the
///  user's description, copied to the clipboard or sent as a new GitHub issue. <see cref="Result"/> is upstream's dialog
///  result: Abort to quit the application, Ignore to go on, Cancel when the window was closed.
/// </summary>
public partial class BugReportWindow : Window
{
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
        ViewModel = new BugReportViewModel(new WindowDialogHost(this), exception, exceptionInfo, environmentInfo,
            canIgnore, showIgnore, focusDetails)
        {
            OpenUrl = OsShellUtil.OpenUrlInDefaultBrowser,
            CopyText = text => Clipboard?.SetTextAsync(text) ?? Task.CompletedTask,
            ShowPropertyValue = (property, information) =>
                _ = new ExceptionDetailWindow(property, information).ShowDialog(this),
        };
        InitializeComponent();
        UpstreamTranslation.Apply(this, BugReportViewModel.Category);

        // After the translation pass, which would leave the title without the application's name.
        DataContext = ViewModel;
        if (ViewModel.ShowsDetails)
        {
            mainTabs.SelectedItem = exceptionTabPage;
        }

        // View concerns: a double click shows a property's whole value, the description takes the focus, and the window
        // cannot be closed while the error cannot be ignored.
        exceptionDetailsListView.DoubleTapped += (_, _) => ViewModel.ShowPropertyCommand.Execute(null);
        ViewModel.DescriptionRequired += (_, _) => descriptionTextBox.Focus();
        Closing += (_, e) => e.Cancel |= !ViewModel.MayClose;
        Opened += (_, _) => descriptionTextBox.Focus();
    }

    public BugReportViewModel ViewModel { get; }

    /// <summary>
    ///  How the window was closed, as upstream's dialog result.
    /// </summary>
    public DialogResult Result => ViewModel.Result;

    /// <summary>
    ///  What the user wrote about the error.
    /// </summary>
    public string Description => ViewModel.Description ?? "";

    /// <summary>
    ///  Opens the new issue's address; tests replace it.
    /// </summary>
    public Action<string> OpenUrl
    {
        get => ViewModel.OpenUrl ?? (_ => { });
        set => ViewModel.OpenUrl = value;
    }
}
