using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using GitCommands;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormOpenDirectory</c>, bound to <see cref="OpenDirectoryViewModel"/>: the repository to open, typed or
///  chosen from upstream's list of folders.
/// </summary>
public partial class OpenDirectoryWindow : Window
{
    /// <summary>
    ///  The design-time constructor (<see cref="DesignTime"/>): the window's XAML only.
    /// </summary>
    [Obsolete(DesignTime.ConstructorOnly)]
#pragma warning disable CS8618 // The fields the window's data fills stay unset: no code of the window runs.
    public OpenDirectoryWindow() => InitializeComponent();
#pragma warning restore CS8618

    /// <param name="directories">Upstream's list (<see cref="OpenDirectoryViewModel.ListDirectories"/>).</param>
    public OpenDirectoryWindow(IReadOnlyList<string> directories)
    {
        ViewModel = new OpenDirectoryViewModel(new WindowDialogHost(this), directories, GitModule.IsValidGitWorkingDir)
        {
            PickFolder = PickFolderAsync,
        };
        DataContext = ViewModel;
        InitializeComponent();
        UpstreamTranslation.Apply(this, OpenDirectoryViewModel.Category);
        InitialFocus.Attach(this, () => _NO_TRANSLATE_Directory);

        // Upstream's folderGoUpButton_Click: the folder box keeps the focus, with the caret after the separator.
        folderGoUpButton.Click += (_, _) => FocusDirectoryEnd();
    }

    public OpenDirectoryViewModel ViewModel { get; }

    private void FocusDirectoryEnd()
    {
        _NO_TRANSLATE_Directory.Focus();
        if (_NO_TRANSLATE_Directory.GetVisualDescendants().OfType<TextBox>().FirstOrDefault() is { } box)
        {
            box.CaretIndex = box.Text?.Length ?? 0;
        }
    }

    // Upstream's OsShellUtil.PickFolder, starting at the folder typed.
    private async Task<string?> PickFolderAsync(string start)
    {
        FolderPickerOpenOptions options = new() { AllowMultiple = false };
        if (Directory.Exists(start))
        {
            options.SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(start);
        }

        IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(options);
        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }
}
