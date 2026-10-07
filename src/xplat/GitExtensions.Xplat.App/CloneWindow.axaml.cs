using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Asks for the source and the target folder of a clone. Closes with a <see cref="CloneRequest"/>, or null when cancelled.
/// </summary>
public partial class CloneWindow : Window
{
    private const string FallbackFolderName = "repo";

    // The target last filled in from the default folder; a target the user typed is not replaced.
    private string _suggestedTarget = "";

    /// <param name="defaultParent">
    ///  Upstream's default clone destination: the new folder is suggested there, named after the repository.
    /// </param>
    public CloneWindow(string defaultParent = "")
    {
        InitializeComponent();
        if (defaultParent.Length > 0)
        {
            UrlBox.PropertyChanged += (_, e) =>
            {
                if (e.Property == TextBox.TextProperty && (TargetBox.Text ?? "") == _suggestedTarget)
                {
                    _suggestedTarget = Path.Combine(defaultParent, RepositoryFolderName(UrlBox.Text ?? ""));
                    TargetBox.Text = _suggestedTarget;
                }
            };
        }

        CancelButton.Click += (_, _) => Close(null);
        CloneButton.Click += (_, _) =>
        {
            if (CreateRequest() is { } request)
            {
                Close(request);
            }
        };
        BrowseButton.Click += (_, _) =>
            UiActions.Run(BrowseAsync, ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));
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
