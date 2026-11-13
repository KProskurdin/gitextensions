using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using GitExtensions.Extensibility;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormCherryPick</c>, bound to <see cref="CherryPickViewModel"/> (rules in <see cref="CherryPickDialog"/>):
///  the commit to pick (another one chosen in upstream's FormChooseCommit), the parent of a merge, whether to commit and to
///  add the reference. Cherry pick runs the caller's pick (upstream's FormProcess and conflict handling) and closes with
///  true; the options are kept then.
/// </summary>
public partial class CherryPickWindow : Window
{
    /// <summary>
    ///  The design-time constructor (<see cref="DesignTime"/>): the window's XAML only.
    /// </summary>
    [Obsolete(DesignTime.ConstructorOnly)]
#pragma warning disable CS8618 // The fields the window's data fills stay unset: no code of the window runs.
    public CherryPickWindow() => InitializeComponent();
#pragma warning restore CS8618

    /// <param name="repositoryPath">The repository.</param>
    /// <param name="revision">The commit to cherry pick.</param>
    /// <param name="preferences">Upstream's two cherry pick options and the commit chooser's grid.</param>
    /// <param name="pick">Runs the cherry pick command and handles its conflicts as upstream does.</param>
    public CherryPickWindow(string repositoryPath, string revision, IAppPreferences preferences,
        Func<Window, string, ArgumentString, Task>? pick)
    {
        ViewModel = new CherryPickViewModel(new WindowDialogHost(this), GitModules.Open(repositoryPath), revision, preferences,
            pick is null ? null : (hash, arguments) => pick(this, hash, arguments),
            async current => (await new ChooseCommitWindow(repositoryPath, current, preferences)
                .ShowDialog<CommitRow?>(this))?.Hash);
        DataContext = ViewModel;
        InitializeComponent();
        DialogHelp.Attach(this, HelpButton, DialogHelp.Manual("modify_history", "cherry-pick-commit"));
        UpstreamTranslation.Apply(this, CherryPickViewModel.Category);
        InitialFocus.Attach(this, () => lvParentsList.IsVisible ? lvParentsList : cbxAutoCommit);
        lvParentsList.ItemTemplate = new FuncDataTemplate<CherryPickParent>((parent, _) => ParentRow(parent));
        Opened += (_, _) => UiActions.Run(ViewModel.LoadAsync, ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));
    }

    public CherryPickViewModel ViewModel { get; }

    /// <summary>
    ///  Completes when the commit and its parents are shown, for tests.
    /// </summary>
    public Task RevisionLoaded => ViewModel.Loaded;

    // Upstream's list columns: No., Message, Author, Date (shared with the revert window).
    internal static Control ParentRow(CherryPickParent? parent)
    {
        Grid row = new() { ColumnDefinitions = new ColumnDefinitions("40,*,120,90") };
        if (parent is null)
        {
            return row;
        }

        string[] texts = [parent.Number.ToString(System.Globalization.CultureInfo.CurrentCulture), parent.Subject,
            parent.Author, parent.Date];
        for (int column = 0; column < texts.Length; column++)
        {
            TextBlock text = new()
            {
                Text = texts[column],
                TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(text, column);
            row.Children.Add(text);
        }

        return row;
    }
}
