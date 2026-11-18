using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's filter toolstrip (<c>FilterToolBar</c>): the advanced filter with its resets, the reflog and first parent
///  toggles, the branch choice with the "Branches:" box, and the "Filter:" box with the kinds of text it filters by. The
///  browse window and the file history show it, each for its own grid (<see cref="Bind"/>). Its state and actions are
///  <see cref="FilterToolBarViewModel"/>'s; the view keeps the keys, the focus, opening the menus, the hotkeys shown in
///  the advanced filter's menu, the branch button's text and the boxes' drop-down widths.
/// </summary>
public partial class FilterToolBar : UserControl
{
    private FilterToolBarViewModel? _viewModel;

    public FilterToolBar()
    {
        InitializeComponent();

        // Upstream's ResizeDropDownWidth on both boxes' lists.
        DropDownWidth.FitToItems(BranchFilterBox);
        DropDownWidth.FitToItems(RevisionFilterBox);
    }

    /// <summary>
    ///  The toolbar's state and actions; set by <see cref="Bind"/>.
    /// </summary>
    public FilterToolBarViewModel ViewModel
        => _viewModel ?? throw new InvalidOperationException($"{nameof(Bind)} is not called.");

    /// <summary>
    ///  Upstream's <c>Bind</c>: the grid the toolbar filters; call <see cref="ShowFilterState"/> when its filter changes.
    /// </summary>
    public void Bind(IFilterToolBarGrid grid, IAppPreferences preferences)
    {
        FilterToolBarViewModel viewModel = new(grid, preferences, new FilterToolBarServices
        {
            Translate = UpstreamTranslation.ByText,
            NoResultsText = () => UpstreamTranslation.Text("TranslatedStrings", "_noResultsFound",
                FilterToolBarServices.DefaultNoResultsText),
            ShowWarningAsync = (text, caption) => TopLevel.GetTopLevel(this) is Window owner
                ? new MessageBoxWindow(text, caption, MessageBoxButtons.OK).ShowDialog(owner)
                : Task.CompletedTask,
            ShowError = message =>
            {
                if (TopLevel.GetTopLevel(this) is Window owner)
                {
                    _ = new ErrorWindow(message).ShowDialog(owner);
                }
            },
        });
        _viewModel = viewModel;
        LayoutRoot.DataContext = viewModel;

        // A flyout gives its items a data context only when it opens (MVVM.md, Flyouts).
        foreach (FlyoutBase? flyout in (FlyoutBase?[])[AdvancedFilterButton.Flyout, ShowBranchesButton.Flyout,
                     BranchTypeButton.Flyout, RevisionFilterTypeButton.Flyout])
        {
            foreach (Control item in ((MenuFlyout)flyout!).Items.OfType<Control>())
            {
                item.DataContext = viewModel;
            }
        }

        viewModel.ResetsMenuRequested += (_, _) => AdvancedFilterButton.Flyout!.ShowAt(AdvancedFilterButton);
        viewModel.PropertyChanged += OnViewModelChanged;
        ShowChosenBranches();

        // Upstream's split button opens its menu.
        ShowBranchesButton.Click += (_, _) => ShowBranchesButton.Flyout!.ShowAt(ShowBranchesButton);

        BranchFilterBox.ItemFilter = (search, item) => FilterToolBarViewModel.IsBranchFilterItemShown(search, item as string,
            viewModel.BranchFilterRefs, viewModel.NoResultsText);
        BranchFilterBox.GotFocus += (_, _) => viewModel.LoadBranchFilterItemsCommand.Execute(null);

        // Upstream's SetShortcutKeys, as the Settings may have changed them.
        ((MenuFlyout)AdvancedFilterButton.Flyout!).Opening += (_, _) =>
        {
            ResetPathFilterMenuItem.InputGesture = Hotkeys.Grid.GestureFor(GridCommand.ResetRevisionPathFilter);
            ResetRevisionFiltersMenuItem.InputGesture = Hotkeys.Grid.GestureFor(GridCommand.ResetRevisionFilter);
            AdvancedFilterMenuItem.InputGesture = Hotkeys.Grid.GestureFor(GridCommand.RevisionFilter);
        };
        BranchFilterBox.AddHandler(KeyUpEvent, (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                viewModel.ApplyBranchFilterCommand.Execute(null);
            }
        }, RoutingStrategies.Bubble, handledEventsToo: true);

        // Enter applies the text; the box's own Enter would only open its list.
        RevisionFilterBox.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                viewModel.ApplyTextFilterCommand.Execute(null);
            }
        }, RoutingStrategies.Tunnel);
    }

    /// <summary>
    ///  Upstream's <c>revisionGridFilter_FilterChanged</c>: the toolbar shows the filter the grid applies.
    /// </summary>
    public void ShowFilterState() => ViewModel.ShowFilterState();

    /// <summary>
    ///  Upstream's <c>ShowAllBranches</c>, <c>ShowCurrentBranchOnly</c> and <c>ShowFilteredBranches</c>.
    /// </summary>
    public void ShowBranches(bool currentOnly, bool filtered)
        => ViewModel.ShowBranches(currentOnly ? BranchesShown.CurrentOnly
            : filtered ? BranchesShown.Filtered
            : BranchesShown.All);

    /// <summary>
    ///  Upstream's <c>SetFocus</c>: the "Filter:" box, or from there the "Branches:" box.
    /// </summary>
    public void FocusFilter()
    {
        if (RevisionFilterBox.IsKeyboardFocusWithin)
        {
            BranchFilterBox.Focus();
        }
        else
        {
            RevisionFilterBox.Focus();
        }
    }

    /// <summary>
    ///  Upstream's <c>SetRevisionFilter</c>: the text in the "Filter:" box, applied (nothing when both are empty).
    /// </summary>
    public void SetRevisionFilter(string? filter) => ViewModel.SetRevisionFilter(filter);

    /// <summary>
    ///  Upstream's <c>BindBranches</c>; see <see cref="FilterToolBarViewModel.IsBranchFilterItemShown"/>.
    /// </summary>
    public static bool IsBranchFilterItemShown(string? search, string? item, IReadOnlyList<string> branches, string noResults)
        => FilterToolBarViewModel.IsBranchFilterItemShown(search, item, branches, noResults);

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FilterToolBarViewModel.ShownBranches))
        {
            ShowChosenBranches();
        }
    }

    // Upstream's SelectShowBranchesFilterOption: the button shows the chosen item's text and tooltip, as translated.
    private void ShowChosenBranches()
    {
        MenuItem chosen = ViewModel.ShownBranches switch
        {
            BranchesShown.CurrentOnly => ShowBranchesCurrentMenuItem,
            BranchesShown.Filtered => ShowBranchesFilteredMenuItem,
            _ => ShowBranchesAllMenuItem,
        };
        ShowBranchesButton.Text = ((string)chosen.Header!).Replace("_", "", StringComparison.Ordinal);
        ToolTip.SetTip(ShowBranchesButton, ToolTip.GetTip(chosen));
    }
}
