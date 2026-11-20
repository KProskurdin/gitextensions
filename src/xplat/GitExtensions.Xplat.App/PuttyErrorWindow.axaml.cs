using Avalonia.Controls;
using Avalonia.Platform.Storage;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Platform;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormPuttyError</c>, bound to <see cref="PuttyErrorViewModel"/>: asks for a PuTTY key after plink failed to
///  authenticate. The view keeps the file picker, the platform's.
/// </summary>
public partial class PuttyErrorWindow : Window
{
    // Upstream BrowseForPrivateKey's filter.
    private const string KeyFilter = " (*.ppk)";
    private const string KeyPattern = "*.ppk";

    public PuttyErrorWindow()
    {
        ViewModel = new PuttyErrorViewModel(new WindowDialogHost(this), PickKeyAsync,
            path => Putty.StartPageantIfConfigured(path, UpstreamTranslation.ByText));
        DataContext = ViewModel;
        InitializeComponent();
        UpstreamTranslation.Apply(this, PuttyErrorViewModel.Category);
    }

    public PuttyErrorViewModel ViewModel { get; }

    /// <summary>
    ///  Retry, with the key loaded if any, or null when the window was cancelled or closed.
    /// </summary>
    public PuttyKeyAnswer? Answer => ViewModel.Answer;

    private async Task<string?> PickKeyAsync()
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = UpstreamTranslation.ByText(CloneViewModel.SshKeyTitle),
            FileTypeFilter = [new FilePickerFileType(KeyFilter) { Patterns = [KeyPattern] }],
        });
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }
}
