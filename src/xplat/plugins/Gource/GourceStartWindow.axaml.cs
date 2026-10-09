using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Plugins.Gource;

/// <summary>
///  The new shell's version of upstream's <c>GourceStart</c>: starts Gource on the repository with the arguments, and keeps
///  the path and arguments in <see cref="GourceStart"/> for the plugin to save. Differences: <c>$(AVATARS)</c> becomes an
///  empty folder, since the new shell has no avatar service yet; and off Windows, where Gource comes from the system's
///  packages, an empty path is filled with the <c>gource</c> found on the PATH.
/// </summary>
public partial class GourceStartWindow : Window
{
    private const string AvatarsVariable = "$(AVATARS)";
    private const string CannotFindGource = "Cannot find Gource.\nPlease download Gource and set the correct path.";
    private const string GourceProgram = "gource";

    private readonly GourceStart _dialog;

    public GourceStartWindow(GourceStart dialog, IGitModule module)
    {
        _dialog = dialog;
        InitializeComponent();
        GourcePathBox.Text = dialog.PathToGource.Length > 0 || OperatingSystem.IsWindows()
            ? dialog.PathToGource
            : FindOnPath(GourceProgram) ?? "";
        WorkingDirBox.Text = dialog.GitWorkingDir ?? module.WorkingDir;
        ArgumentsBox.Text = dialog.GourceArguments;
        StartButton.Click += (_, _) => Start();
        GourceBrowseButton.Click += (_, _) => _ = BrowseGourceAsync();
        WorkingDirBrowseButton.Click += (_, _) => _ = BrowseWorkingDirAsync();
    }

    // Upstream's Button1Click.
    private void Start()
    {
        string path = GourcePathBox.Text ?? "";
        if (!File.Exists(path))
        {
            MessageBoxes.ShowError(new WindowOwner(this), CannotFindGource);
            return;
        }

        _dialog.GourceArguments = ArgumentsBox.Text ?? "";
        string arguments = _dialog.GourceArguments.Contains(AvatarsVariable)
            ? _dialog.GourceArguments.Replace(AvatarsVariable, EmptyAvatarsFolder())
            : _dialog.GourceArguments;
        _dialog.PathToGource = path;
        _dialog.GitWorkingDir = WorkingDirBox.Text;

        try
        {
            Process.Start(new ProcessStartInfo(path, arguments) { WorkingDirectory = WorkingDirBox.Text ?? "" });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            MessageBoxes.ShowError(new WindowOwner(this), ex.Message);
        }

        Close();
    }

    // Upstream's folder for the authors' pictures, emptied first as upstream does.
    private static string EmptyAvatarsFolder()
    {
        string folder = Path.Join(Path.GetTempPath(), "GitAvatars");
        Directory.CreateDirectory(folder);
        foreach (string file in Directory.GetFiles(folder))
        {
            File.Delete(file);
        }

        return folder;
    }

    private static string? FindOnPath(string program)
        => (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(folder => Path.Join(folder, program))
            .FirstOrDefault(File.Exists);

    private async Task BrowseGourceAsync()
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Gource",
            AllowMultiple = false,
        });
        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path)
        {
            GourcePathBox.Text = path;
        }
    }

    private async Task BrowseWorkingDirAsync()
    {
        IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
        });
        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path)
        {
            WorkingDirBox.Text = path;
        }
    }
}
