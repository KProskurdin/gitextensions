using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using GitExtensions.Xplat.Core.Diff;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  The view of upstream FileStatusList's "Find in commit files using git-grep" (<see cref="GitGrepViewModel"/>) for a list
///  that shows a commit's files: the box below the list's filter, its delete button, the toolbar's split button and its
///  menu are bound to the view model, and upstream's window (<see cref="FindInCommitFilesGitGrepWindow"/>) is opened for it.
///  Focusing the box when it is shown stays here.
/// </summary>
public sealed class GitGrepBox : IFileListGitGrep
{
    /// <param name="search">Reads the list again with a git-grep argument ("" for none).</param>
    internal GitGrepBox(Window owner, ComboBox box, Button delete, GitGrepButton? button, IAppPreferences preferences,
        Action<string> search)
    {
        ViewModel = new GitGrepViewModel(preferences, search,
            viewModel => new GrepWindow(owner, preferences, viewModel), UpstreamTranslation.ByText);
        box.ItemsSource = ViewModel.Searches;
        box.PlaceholderText = UpstreamTranslation.ByText(GitGrep.Watermark);
        Bind(box, Visual.IsVisibleProperty, nameof(GitGrepViewModel.IsBoxVisible));

        // As an editable list must, IsEditable is set before its text is bound (MVVM.md, Editable lists).
        Bind(box, ComboBox.TextProperty, nameof(GitGrepViewModel.BoxText), BindingMode.TwoWay);
        Bind(box, SelectingItemsControl.SelectedItemProperty, nameof(GitGrepViewModel.PickedSearch), BindingMode.TwoWay);
        Bind(delete, Visual.IsVisibleProperty, nameof(GitGrepViewModel.IsDeleteVisible));
        delete.Command = ViewModel.ClearCommand;
        ViewModel.BoxFocusRequested += (_, _) => box.Focus();
        button?.Bind(ViewModel);
    }

    public GitGrepViewModel ViewModel { get; }

    /// <summary>
    ///  The search in effect.
    /// </summary>
    public string Text => ViewModel.Searched;

    /// <summary>
    ///  Completes when the search asked for last has been handed to the list, for tests.
    /// </summary>
    public Task Searched => ViewModel.SearchCompleted;

    /// <summary>
    ///  Whether the list shows the box.
    /// </summary>
    public bool BoxVisible => ViewModel.IsBoxVisible;

    /// <summary>
    ///  The window while it is open, for tests.
    /// </summary>
    public FindInCommitFilesGitGrepWindow? Window => ((GrepWindow)ViewModel.Window).Open;

    public void SetBoxVisible(bool visible) => ViewModel.SetBoxVisible(visible);

    public void ShowWindow(string text) => ViewModel.ShowWindow(text);

    public void Toggle(bool fromButton) => ViewModel.Toggle(fromButton);

    private void Bind(Control control, AvaloniaProperty property, string path,
        BindingMode mode = BindingMode.OneWay)
        => control.Bind(property, new Binding(path) { Source = ViewModel, Mode = mode });

    /// <summary>
    ///  The list toolbar's git-grep split button (upstream's <c>btnFindInFilesGitGrep</c>) and its menu.
    /// </summary>
    internal sealed record GitGrepButton(SplitButton Button, MenuItem MatchCase, MenuItem WholeWord, MenuItem Options,
        MenuItem UsingWindow, MenuItem UsingBox, MenuItem UsingBoth)
    {
        // The button sits in a toolbar bound to another view model, and its flyout's items have no data context until it
        // opens (MVVM.md, Flyouts), so each gets this one.
        public void Bind(GitGrepViewModel viewModel)
        {
            Button.DataContext = viewModel;
            Button.Command = viewModel.ToggleFromButtonCommand;
            Check(MatchCase, nameof(GitGrepViewModel.MatchCase), viewModel.ToggleMatchCaseCommand);
            Check(WholeWord, nameof(GitGrepViewModel.MatchWholeWord), viewModel.ToggleMatchWholeWordCommand);
            Options.DataContext = viewModel;
            Options.Bind(HeaderedSelectingItemsControl.HeaderProperty,
                new Binding(nameof(GitGrepViewModel.OptionsHeader)) { Mode = BindingMode.OneWay });
            Options.ItemsSource = viewModel.PatternOptionEntries.Select(entry => MenuEntries.Item(entry, Button)).ToList();
            Check(UsingWindow, nameof(GitGrepViewModel.UsesWindow), viewModel.SetFindUsingCommand, GitGrepViewModel.UsingWindow);
            Check(UsingBox, nameof(GitGrepViewModel.UsesBox), viewModel.SetFindUsingCommand, GitGrepViewModel.UsingBox);
            Check(UsingBoth, nameof(GitGrepViewModel.UsesBoth), viewModel.SetFindUsingCommand, GitGrepViewModel.UsingBoth);
            if (Button.Flyout is MenuFlyout flyout)
            {
                flyout.Opening += (_, _) => viewModel.RefreshMenu();
            }

            return;

            // A check item: checked one way, as the command stores the setting (MVVM.md, Menu check items).
            void Check(MenuItem item, string path, System.Windows.Input.ICommand command, object? parameter = null)
            {
                item.DataContext = viewModel;
                item.Bind(MenuItem.IsCheckedProperty, new Binding(path) { Mode = BindingMode.OneWay });
                item.Command = command;
                item.CommandParameter = parameter;
            }
        }
    }

    // Upstream's FormFindInCommitFilesGitGrep over the list's window, made when it is shown and forgotten when it closes.
    private sealed class GrepWindow(Window owner, IAppPreferences preferences, GitGrepViewModel viewModel) : IGitGrepWindow
    {
        private FindInCommitFilesGitGrepWindow? _window;

        public FindInCommitFilesGitGrepWindow? Open => _window is { IsVisible: true } window ? window : null;

        public bool IsOpen => Open is not null;

        public void Show()
        {
            if (Open is null)
            {
                FindInCommitFilesGitGrepWindow window = new(preferences, viewModel.SearchFromWindow,
                    viewModel.SetBoxVisibleFromWindow);
                window.Closed += (_, _) =>
                {
                    if (_window == window)
                    {
                        _window = null;
                    }
                };
                _window = window;
                window.Show(owner);
            }

            _window!.Activate();
        }

        public void Close() => _window?.Close();

        public void SetExpression(string text) => _window?.GitGrepExpressionText = text;

        public void SetSearchItems(IReadOnlyList<string> searches) => _window?.SetSearchItems(searches);

        public void SetShowBox(bool visible) => _window?.SetShowFindInCommitFilesGitGrep(visible);
    }
}
