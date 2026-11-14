using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormGoToCommit</c>, bound to <see cref="GoToCommitViewModel"/> (rules in <see cref="GoToCommit"/>): a
///  commit expression, a tag or a branch to go to. The control last focused supplies the revision. Closes with the commit
///  git resolves it to (zero when git knows none) after Go, with null otherwise.
/// </summary>
public partial class GoToCommitWindow : Window
{
    private bool _pickedInDropDown;

    /// <summary>
    ///  The design-time constructor (<see cref="DesignTime"/>): the window's XAML only.
    /// </summary>
    [Obsolete(DesignTime.ConstructorOnly)]
#pragma warning disable CS8618 // The fields the window's data fills stay unset: no code of the window runs.
    public GoToCommitWindow() => InitializeComponent();
#pragma warning restore CS8618

    public GoToCommitWindow(string repositoryPath)
    {
        ViewModel = new GoToCommitViewModel(new WindowDialogHost(this), GitModules.Open(repositoryPath),
            async () => Clipboard is { } clipboard ? await clipboard.TryGetTextAsync() : null);
        DataContext = ViewModel;
        InitializeComponent();
        UpstreamTranslation.Apply(this, GoToCommitViewModel.Category);
        InitialFocus.Attach(this, () => textboxCommitExpression);

        // View concerns: the focused control supplies the revision (upstream's SetSelectedRevisionByFocusedControl), a ref
        // picked in a drop-down goes at once (upstream's SelectionChangeCommitted), Enter in a list goes, and the link
        // opens the browser.
        textboxCommitExpression.GotFocus += (_, _) => ViewModel.Source = GoToCommitSource.Expression;
        TrackDropDown(comboBoxTags, GoToCommitSource.Tag);
        TrackDropDown(comboBoxBranches, GoToCommitSource.Branch);
        linkGitRevParse.Click += (_, _) =>
            UiActions.Run(() => Launcher.LaunchUriAsync(new Uri(GoToCommit.GitRevParseUrl)),
                ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));
        Opened += (_, _) => _ = ShowAsync();
    }

    public GoToCommitViewModel ViewModel { get; }

    /// <summary>
    ///  Completes when the tags and branches are listed and the clipboard was read, for tests.
    /// </summary>
    public Task RefsLoaded => ViewModel.Loaded;

    private async Task ShowAsync()
    {
        await ViewModel.LoadAsync();
        if (ViewModel.ExpressionFromClipboard)
        {
            textboxCommitExpression.SelectAll();
        }
    }

    private void TrackDropDown(ComboBox combo, GoToCommitSource source)
    {
        combo.GotFocus += (_, _) => ViewModel.Source = source;
        combo.SelectionChanged += (_, _) => _pickedInDropDown |= combo.IsDropDownOpen && combo.SelectedItem is not null;
        combo.DropDownClosed += (_, _) =>
        {
            if (_pickedInDropDown)
            {
                ViewModel.Source = source;
                ViewModel.GoCommand.Execute(null);
            }
        };
        combo.KeyUp += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                ViewModel.GoCommand.Execute(null);
            }
        };
    }
}
