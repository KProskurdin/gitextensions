using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>CommitInfo</c> and <c>CommitInfoHeader</c>: a commit's header, message and the information read after it
///  (annotated tags, related links, the branches and tags containing it, the tag it derives from), with upstream's menu;
///  bound to <see cref="CommitInfoViewModel"/>. Shared by the browse window, upstream's <c>CommitDiff</c>
///  (<see cref="CommitDiffView"/>) and the blame view.
/// </summary>
public partial class CommitInfoView : UserControl
{
    public CommitInfoView()
    {
        InitializeComponent();
        ViewModel = new CommitInfoViewModel(AppServices.Preferences, translate: UpstreamTranslation.ByText)
        {
            SetClipboard = text => TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard
                ? clipboard.SetTextAsync(text)
                : Task.CompletedTask,
            OpenUri = address => TopLevel.GetTopLevel(this)?.Launcher is { } launcher
                ? launcher.LaunchUriAsync(address)
                : Task.CompletedTask,
        };
        RevisionInfoScroll.DataContext = ViewModel;

        // A context menu gets its data context only when it opens (MVVM.md).
        CommitInfoContextMenu.DataContext = ViewModel;
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CommitInfoViewModel.HeaderRows))
            {
                ShowHeader(ViewModel.HeaderRows);
            }
        };
        DetailMessage.LinkClicked += (_, link) => ViewModel.OpenLinkCommand.Execute(link);
        DetailRevisionInfo.LinkClicked += (_, link) => ViewModel.OpenLinkCommand.Execute(link);

        // The link under the pointer, read before the menu opens (it opens while the request bubbles).
        RevisionInfoScroll.AddHandler(ContextRequestedEvent, (_, e) => ViewModel.MenuLink = LinkAt(e),
            RoutingStrategies.Tunnel);
        CommitInfoContextMenu.Opening += (_, _) => ViewModel.RefreshMenu();
    }

    public CommitInfoViewModel ViewModel { get; }

    /// <inheritdoc cref="CommitInfoViewModel.RevisionLinkClicked"/>
    public event EventHandler<string>? RevisionLinkClicked
    {
        add => ViewModel.RevisionLinkClicked += value;
        remove => ViewModel.RevisionLinkClicked -= value;
    }

    /// <inheritdoc cref="CommitInfoViewModel.AddNotesRequested"/>
    public event EventHandler? AddNotesRequested
    {
        add => ViewModel.AddNotesRequested += value;
        remove => ViewModel.AddNotesRequested -= value;
    }

    /// <summary>
    ///  The hotkey shown beside "Add notes" (upstream's <c>FormBrowse.Command.AddNotes</c>).
    /// </summary>
    public KeyGesture? AddNotesGesture
    {
        get => addNoteToolStripMenuItem.InputGesture;
        set => addNoteToolStripMenuItem.InputGesture = value;
    }

    /// <inheritdoc cref="CommitInfoViewModel.Preferences"/>
    public IAppPreferences Preferences
    {
        get => ViewModel.Preferences;
        set => ViewModel.Preferences = value;
    }

    /// <inheritdoc cref="CommitInfoViewModel.RepositoryPath"/>
    public string? RepositoryPath
    {
        get => ViewModel.RepositoryPath;
        set => ViewModel.RepositoryPath = value;
    }

    /// <inheritdoc cref="CommitInfoViewModel.Details"/>
    public CommitDetails? Details => ViewModel.Details;

    /// <inheritdoc cref="CommitInfoViewModel.UserRefSorting"/>
    internal static RefSorting UserRefSorting(IAppPreferences preferences) => CommitInfoViewModel.UserRefSorting(preferences);

    /// <inheritdoc cref="CommitInfoViewModel.Show"/>
    public void Show(CommitDetails? details, string? error, IReadOnlyList<string> childHashes)
        => ViewModel.Show(details, error, childHashes);

    /// <inheritdoc cref="CommitInfoViewModel.ShowAvatar"/>
    public void ShowAvatar() => ViewModel.ShowAvatar();

    // Upstream's CommitInfoHeader: the labels in a column as wide as the longest, their values beside them.
    private void ShowHeader(IReadOnlyList<InfoHeaderRow> rows)
    {
        DetailHeader.Children.Clear();
        DetailHeader.RowDefinitions.Clear();
        for (int i = 0; i < rows.Count; i++)
        {
            DetailHeader.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            TextBlock label = new() { Text = rows[i].Label, Margin = new Thickness(0, 0, 12, 0) };
            InfoTextBlock value = new() { Lines = [rows[i].Value], TextWrapping = TextWrapping.Wrap };
            value.LinkClicked += (_, link) => ViewModel.OpenLinkCommand.Execute(link);
            Grid.SetRow(label, i);
            Grid.SetRow(value, i);
            Grid.SetColumn(value, 1);
            DetailHeader.Children.Add(label);
            DetailHeader.Children.Add(value);
        }
    }

    private static string? LinkAt(ContextRequestedEventArgs e)
    {
        InfoTextBlock? block = (e.Source as Visual)?.FindAncestorOfType<InfoTextBlock>(includeSelf: true);
        return block is not null && e.TryGetPosition(block, out Point point) ? block.LinkAt(point) : null;
    }
}
