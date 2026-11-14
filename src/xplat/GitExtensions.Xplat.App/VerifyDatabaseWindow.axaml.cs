using System.ComponentModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormVerify</c>, bound to <see cref="VerifyDatabaseViewModel"/> (Repository > Git maintenance > Recover
///  lost objects...; rules in <see cref="VerifyDatabase"/>): the objects <c>git fsck-objects</c> finds with the chosen
///  options, their content, and upstream's ways to recover them.
/// </summary>
public partial class VerifyDatabaseWindow : Window
{
    private const double HashColumnWidth = 60;
    private const double AuthorColumnWidth = 150;

    public VerifyDatabaseWindow()
        : this("", _ => Task.FromResult(false), (_, _) => Task.FromResult(false), (_, _) => Task.FromResult(false))
    {
    }

    /// <param name="repositoryPath">The repository whose objects are checked.</param>
    /// <param name="runGitWithOutput">
    ///  Runs a git command with its output shown (upstream's FormProcess), for "Save objects to .git/lost-found" and
    ///  "Remove all dangling objects".
    /// </param>
    /// <param name="createTag">Opens the create tag window on an object, true when a tag was created.</param>
    /// <param name="createBranch">Opens the create branch window on a commit, true when a branch was created.</param>
    public VerifyDatabaseWindow(string repositoryPath, Func<string, Task<bool>> runGitWithOutput,
        Func<Window, ObjectId, Task<bool>> createTag, Func<Window, ObjectId, Task<bool>> createBranch)
    {
        string workingDirectory = repositoryPath;
        ViewModel = new VerifyDatabaseViewModel(new WindowDialogHost(this), GitModules.Open(repositoryPath),
            runGitWithOutput, id => createTag(this, id), id => createBranch(this, id))
        {
            ViewText = (content, fileName) => new ViewTextWindow(content, fileName).ShowDialog(this),
            CopyText = text => Clipboard?.SetTextAsync(text) ?? Task.CompletedTask,
            PickSaveFile = (fileName, extensions) => PickSaveFileAsync(workingDirectory, fileName, extensions),
        };
        DataContext = ViewModel;
        InitializeComponent();
        UpstreamTranslation.Apply(this, VerifyDatabaseViewModel.Category);
        InitialFocus.Attach(this, () => Warnings);

        // View concerns: the rows' columns, the wait while git checks, the preview in the viewer, the double click and the
        // menu's keys.
        mnuLostObjects.DataContext = ViewModel;
        Warnings.ItemTemplate = new FuncDataTemplate<LostObjectRow>((row, _) => Row(row));
        ViewModel.PropertyChanged += OnViewModelChanged;
        Warnings.DoubleTapped += (_, e) =>
        {
            // Upstream ignores a double click on the check box, which the user meant to toggle.
            if ((e.Source as Visual)?.FindAncestorOfType<CheckBox>(includeSelf: true) is null)
            {
                ViewModel.ViewCommand.Execute(null);
            }
        };
        Warnings.AddHandler(KeyDownEvent, OnWarningsKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);
        mnuLostObjects.Opening += (_, e) => e.Cancel = ViewModel.SelectedRow is null;
        Opened += (_, _) => ViewModel.Reload();
    }

    public VerifyDatabaseViewModel ViewModel { get; }

    /// <summary>
    ///  The last read of the lost objects, for tests.
    /// </summary>
    public Task ObjectsLoaded => ViewModel.ObjectsLoaded;

    /// <summary>
    ///  The last preview of the selected object, for tests.
    /// </summary>
    public Task PreviewShown => ViewModel.PreviewShown;

    /// <summary>
    ///  The objects as listed (after the "Show ..." filters, in the sort order).
    /// </summary>
    public IReadOnlyList<LostObjectRow> Rows => ViewModel.Rows;

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(VerifyDatabaseViewModel.IsLoading):
                IsEnabled = !ViewModel.IsLoading;
                Cursor = ViewModel.IsLoading ? new Cursor(StandardCursorType.Wait) : Cursor.Default;
                break;
            case nameof(VerifyDatabaseViewModel.ShowCommitsAndTags):
                // The commit columns go with the commits.
                bool commitsAndTags = ViewModel.ShowCommitsAndTags;
                WarningsHeader.ColumnDefinitions[VerifyDatabaseViewModel.AuthorColumn].Width =
                    new GridLength(commitsAndTags ? AuthorColumnWidth : 0);
                WarningsHeader.ColumnDefinitions[VerifyDatabaseViewModel.ParentColumn].Width =
                    new GridLength(commitsAndTags ? HashColumnWidth : 0);
                break;
            case nameof(VerifyDatabaseViewModel.Preview) when ViewModel.Preview is { } preview:
                if (preview.IsPatch)
                {
                    fileViewer.ShowPatch(preview.FileName, preview.Content);
                }
                else
                {
                    fileViewer.ShowText(preview.FileName, preview.Content);
                }

                break;
        }
    }

    // Upstream's Warnings_KeyDown: Enter views the object.
    private void OnWarningsKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None)
        {
            e.Handled = true;
            ViewModel.ViewCommand.Execute(null);
        }
    }

    // Upstream's ProcessKeyPreview: the menu's shortcuts work while the menu is closed, for the items it would enable.
    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers != Hotkeys.CommandModifier || ViewModel.SelectedRow is null)
        {
            return;
        }

        ICommand? command = e.Key switch
        {
            Key.T when ViewModel.SelectedIsCommit => ViewModel.CreateTagCommand,
            Key.B when ViewModel.SelectedIsCommit => ViewModel.CreateBranchCommand,
            Key.S when ViewModel.SelectedIsBlob => ViewModel.SaveAsCommand,
            _ => null
        };
        if (command is not null)
        {
            e.Handled = true;
            command.Execute(null);
        }
    }

    // Upstream's saveAsToolStripMenuItem_Click: the blob's guessed name, offering its type's equivalents.
    private async Task<string?> PickSaveFileAsync(string workingDirectory, string fileName,
        IReadOnlyList<string> extensions)
    {
        IStorageFile? file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(workingDirectory),
            SuggestedFileName = fileName,
            DefaultExtension = extensions[0],
            FileTypeChoices =
            [
                .. extensions.Select(extension => new FilePickerFileType($"{extension} Files")
                {
                    Patterns = [$"*.{extension}"],
                }),
                new FilePickerFileType("All Files") { Patterns = ["*.*"] },
            ],
        });
        return file?.TryGetLocalPath();
    }

    private Control Row(LostObjectRow? row)
    {
        bool commitsAndTags = ViewModel.ShowCommitsAndTags;
        Grid grid = new()
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(24, GridUnitType.Pixel),
                new ColumnDefinition(GridLength.Auto) { SharedSizeGroup = "Date" },
                new ColumnDefinition(GridLength.Auto) { SharedSizeGroup = "Type" },
                new ColumnDefinition(1, GridUnitType.Star),
                new ColumnDefinition(commitsAndTags ? AuthorColumnWidth : 0, GridUnitType.Pixel),
                new ColumnDefinition(HashColumnWidth, GridUnitType.Pixel),
                new ColumnDefinition(commitsAndTags ? HashColumnWidth : 0, GridUnitType.Pixel),
            },
        };
        if (row is null)
        {
            return grid;
        }

        CheckBox check = new()
        {
            MinWidth = 0, Padding = new(0), VerticalAlignment = VerticalAlignment.Top, DataContext = row
        };
        check.Bind(CheckBox.IsCheckedProperty,
            CompiledBinding.Create<LostObjectRow, bool>(lost => lost.IsSelected, mode: BindingMode.TwoWay));
        grid.Children.Add(check);
        LostObject lostObject = row.Object;
        AddCell(VerifyDatabaseViewModel.DateColumn, lostObject.Date?.ToString() ?? "", wrap: false);
        AddCell(VerifyDatabaseViewModel.TypeColumn, lostObject.RawType, wrap: false);
        AddCell(VerifyDatabaseViewModel.SubjectColumn, lostObject.Subject ?? "", wrap: false);
        AddCell(VerifyDatabaseViewModel.AuthorColumn, lostObject.Author ?? "", wrap: false);
        AddCell(VerifyDatabaseViewModel.HashColumn, lostObject.ObjectId.ToString(), wrap: true);
        AddCell(VerifyDatabaseViewModel.ParentColumn, lostObject.Parent.IsZero ? "" : lostObject.Parent.ToString(),
            wrap: true);
        return grid;

        void AddCell(int column, string text, bool wrap)
        {
            TextBlock cell = new()
            {
                Text = text,
                Margin = new(0, 0, 6, 0),
                TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
                TextTrimming = wrap ? TextTrimming.None : TextTrimming.CharacterEllipsis,
            };
            Grid.SetColumn(cell, column);
            grid.Children.Add(cell);
        }
    }
}
