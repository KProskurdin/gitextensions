using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Asks for the source and the target folder of a clone. Closes with a <see cref="CloneRequest"/>, or null when cancelled.
/// </summary>
public partial class CloneWindow : Window
{
    private const string FallbackFolderName = "repo";

    public CloneWindow()
    {
        InitializeComponent();
        CancelButton.Click += (_, _) => Close(null);
        CloneButton.Click += (_, _) =>
        {
            if (CreateRequest() is { } request)
            {
                Close(request);
            }
        };
        BrowseButton.Click += (_, _) => UiActions.Run(BrowseAsync, ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));
    }

    /// <summary>
    ///  Picks the folder that will contain the clone, and names the new folder after the repository in the URL.
    /// </summary>
    private async Task BrowseAsync()
    {
        IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Clone into folder", AllowMultiple = false,
        });

        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } parent)
        {
            TargetBox.Text = Path.Combine(parent, RepositoryFolderName(UrlBox.Text ?? ""));
        }
    }

    private static string RepositoryFolderName(string url)
    {
        string name = Path.GetFileNameWithoutExtension(url.Trim().TrimEnd('/', '\\'));
        return name.Length == 0 ? FallbackFolderName : name;
    }

    private CloneRequest? CreateRequest()
    {
        string url = UrlBox.Text?.Trim() ?? "";
        string target = TargetBox.Text?.Trim() ?? "";
        return url.Length == 0 || target.Length == 0 ? null : new CloneRequest(url, target);
    }
}

public sealed record CloneRequest(string Url, string TargetPath);
