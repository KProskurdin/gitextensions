using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Version and platform information, like upstream's <c>FormAbout</c>.
/// </summary>
public partial class AboutWindow : Avalonia.Controls.Window
{
    public AboutWindow(string gitVersion)
    {
        InitializeComponent();
        LogoImage.Source = new Bitmap(AssetLoader.Open(new Uri("avares://GitExtensions/Assets/git-extensions-logo-256px.png")));
        string version = typeof(AboutWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                         ?? typeof(AboutWindow).Assembly.GetName().Version?.ToString()
                         ?? "unknown";
        VersionText.Text = $"Version {version}\n{RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture}), {RuntimeInformation.FrameworkDescription}\n{gitVersion}";
        CloseButton.Click += (_, _) => Close();
    }
}
