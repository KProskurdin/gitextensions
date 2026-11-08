using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using GitCommands;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Ui;
using GitExtUtils;

namespace GitExtensions.Plugins.Gource;

/// <summary>
///  The new shell's version of upstream's <c>GourceStart</c>: starts Gource on the repository with the arguments, and keeps
///  the path and arguments in <see cref="GourceStart"/> for the plugin to save. Differences: the authors' pictures are read
///  without blocking the window; and off Windows, where Gource comes from the system's packages, an empty path is filled
///  with the <c>gource</c> found on the PATH.
/// </summary>
public partial class GourceStartWindow : Window
{
    private const string AvatarsVariable = "$(AVATARS)";
    private const string CannotFindGource = "Cannot find Gource.\nPlease download Gource and set the correct path.";
    private const string GourceProgram = "gource";
    private const int AvatarSize = 90;

    private static readonly byte[] PngSignature = [0x89, (byte)'P', (byte)'N', (byte)'G'];

    private readonly GourceStart _dialog;
    private readonly IGitModule _module;

    public GourceStartWindow(GourceStart dialog, IGitModule module)
    {
        _dialog = dialog;
        _module = module;
        InitializeComponent();
        UpstreamTranslation.Apply(this, "GourceStart");
        GourcePathBox.Text = dialog.PathToGource.Length > 0 || OperatingSystem.IsWindows()
            ? dialog.PathToGource
            : FindOnPath(GourceProgram) ?? "";
        WorkingDirBox.Text = dialog.GitWorkingDir ?? module.WorkingDir;
        ArgumentsBox.Text = dialog.GourceArguments;
        button1.Click += (_, _) => _ = StartAsync();
        GourceBrowse.Click += (_, _) => _ = BrowseGourceAsync();
        WorkingDirBrowse.Click += (_, _) => _ = BrowseWorkingDirAsync();
    }

    // Upstream's Button1Click.
    private async Task StartAsync()
    {
        string path = GourcePathBox.Text ?? "";
        if (!File.Exists(path))
        {
            MessageBoxes.ShowError(new WindowOwner(this), CannotFindGource);
            return;
        }

        _dialog.GourceArguments = ArgumentsBox.Text ?? "";
        _dialog.PathToGource = path;
        _dialog.GitWorkingDir = WorkingDirBox.Text;
        string workingDirectory = WorkingDirBox.Text ?? "";
        button1.IsEnabled = false;

        try
        {
            string avatars = _dialog.GourceArguments.Contains(AvatarsVariable) ? await LoadAvatarsAsync() : "";
            string arguments = _dialog.GourceArguments.Replace(AvatarsVariable, avatars);
            Process.Start(new ProcessStartInfo(path, arguments) { WorkingDirectory = workingDirectory });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException
            or IOException or UnauthorizedAccessException)
        {
            MessageBoxes.ShowError(new WindowOwner(this), ex.Message);
        }

        Close();
    }

    // Upstream's LoadAvatarsAsync: the folder, emptied first, with a picture per author name, as Gource looks them up. An
    // author without a picture is left to Gource.
    private async Task<string> LoadAvatarsAsync()
    {
        string folder = Path.Join(Path.GetTempPath(), "GitAvatars");
        Directory.CreateDirectory(folder);
        foreach (string file in Directory.GetFiles(folder))
        {
            File.Delete(file);
        }

        GitArgumentBuilder args = new("log") { "--pretty=format:\"%aE|%aN\"" };
        string output = await Task.Run(() => _module.GitExecutable.GetOutput(args));
        IEnumerable<(string Email, string Name)> authors = output.Split('\n')
            .Select(line => line.Split('|'))
            .Where(bits => bits.Length > 1 && !string.IsNullOrWhiteSpace(bits[0]) && !string.IsNullOrWhiteSpace(bits[1]))
            .GroupBy(bits => bits[1])
            .Select(group => (group.First()[0], group.Key));
        await Task.WhenAll(authors.Select(author => SaveAvatarAsync(folder, author.Email, author.Name)));
        return folder;
    }

    private static async Task SaveAvatarAsync(string folder, string email, string name)
    {
        try
        {
            byte[]? image = PluginAvatars.GetImage is { } getImage ? await getImage(email, name, AvatarSize) : null;
            string fileName = name + ".png";
            if (image is null || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                return;
            }

            string filePath = Path.Join(folder, fileName);
            if (image.AsSpan().StartsWith(PngSignature))
            {
                await File.WriteAllBytesAsync(filePath, image);
                return;
            }

            // The avatar settings' providers send JPEG and others too; Bitmap.Save writes PNG.
            using MemoryStream stream = new(image);
            using Avalonia.Media.Imaging.Bitmap bitmap = new(stream);
            bitmap.Save(filePath);
        }
        catch
        {
            // As upstream: a picture that cannot be read or written leaves the author to Gource.
        }
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
