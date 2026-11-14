using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using GitCommands;
using GitCommands.Remotes;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Platform;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormRemotes</c> in the new shell, bound to <see cref="RemotesViewModel"/> (rules in
///  <see cref="RemotesDialog"/>): the remotes (active and inactive) with the details of the selected one, and the default
///  pull behavior of the local branches.
/// </summary>
public partial class RemotesWindow : Window
{
    // WinForms' ColorDialog basic colors, which upstream's "Set color" offers first.
    private static readonly string[] _basicColors =
    [
        "#FF8080", "#FFFF80", "#80FF80", "#00FF80", "#80FFFF", "#0080FF", "#FF80C0", "#FF80FF",
        "#FF0000", "#FFFF00", "#80FF00", "#00FF40", "#00FFFF", "#0080C0", "#8080C0", "#FF00FF",
        "#804040", "#FF8040", "#00FF00", "#008080", "#004080", "#8080FF", "#800040", "#FF0080",
        "#800000", "#FF8000", "#008000", "#008040", "#0000FF", "#0000A0", "#800080", "#8000FF",
        "#400000", "#804000", "#004000", "#004040", "#000080", "#000040", "#400040", "#400080",
        "#000000", "#808000", "#808040", "#808080", "#408080", "#C0C0C0", "#400040", "#FFFFFF",
    ];

    /// <summary>
    ///  The design-time constructor (<see cref="DesignTime"/>): the window's XAML only.
    /// </summary>
    [Obsolete(DesignTime.ConstructorOnly)]
#pragma warning disable CS8618 // The fields the window's data fills stay unset: no code of the window runs.
    public RemotesWindow() => InitializeComponent();
#pragma warning restore CS8618

    /// <param name="fetchRemote">Fetches a saved remote, as upstream pulls with the fetch action after a save.</param>
    /// <param name="preselectRemote">Upstream's <c>PreselectRemoteOnLoad</c>.</param>
    /// <param name="preselectLocal">Upstream's <c>PreselectLocalOnLoad</c>: opens the pull behavior tab on this branch.</param>
    public RemotesWindow(string repositoryPath, IAppPreferences preferences, Func<string, Task>? fetchRemote = null,
        string? preselectRemote = null, string? preselectLocal = null)
    {
        GitModule module = GitModules.Open(repositoryPath);
        ViewModel = new RemotesViewModel(new WindowDialogHost(this), module,
            new RemotesDialog(new ConfigFileRemoteSettingsManager(() => module), AppSettings.CustomGenericRemoteNames),
            preferences, AppServices.RemoteUrls, fetchRemote, preselectRemote, preselectLocal)
        {
            PickFolder = PickFolderAsync, PickPuttyKey = PickPuttyKeyAsync,
        };
        DataContext = ViewModel;
        InitializeComponent();

        // View concerns: PuTTY's panel, the color swatches and the chosen color, the toggle's eye, the focus and the boxes'
        // proposals when they get the focus, and the branch's remote and merge branch written when their box is left.
        PuttyPanel.IsVisible = GitSshHelpers.IsPlink;
        foreach (string color in _basicColors)
        {
            ColorSwatches.Children.Add(new Button
            {
                Width = 26,
                Height = 22,
                Margin = new Avalonia.Thickness(1),
                Padding = new Avalonia.Thickness(0),
                Background = Brush.Parse(color),
                Command = ViewModel.ChooseColorCommand,
                CommandParameter = color,
            });
        }

        ShowColor();
        ShowToggleState();
        ViewModel.PropertyChanged += OnViewModelChanged;
        ViewModel.NameRequired += (_, _) => NameBox.Focus();
        NameBox.GotFocus += (_, _) =>
        {
            if (ViewModel.ProposeName())
            {
                NameBox.SelectAll();
            }
        };
        UrlBox.GotFocus += (_, _) => ViewModel.ProposeUrls(pushUrl: false);
        PushUrlBox.GotFocus += (_, _) => ViewModel.ProposeUrls(pushUrl: true);
        PrefixBox.LostFocus += (_, _) => ViewModel.NormalisePrefix();
        ColorHtmlBox.KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Enter && Color.TryParse(ColorHtmlBox.Text, out Color color))
            {
                ViewModel.ChooseColorCommand.Execute($"#{color.R:X2}{color.G:X2}{color.B:X2}");
            }
        };
        RemoteRepositoryBox.LostFocus += (_, _) => ViewModel.StoreBranchRemote();
        MergeWithBox.LostFocus += (_, _) => ViewModel.StoreMergeWith();
        MergeWithBox.DropDownOpened += (_, _) => ViewModel.FillMergeWithChoices();
        Opened += (_, _) => _ = ViewModel.LoadAsync();
        KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Escape)
            {
                Close();
            }
        };
    }

    public RemotesViewModel ViewModel { get; }

    /// <summary>
    ///  The dialog state, for tests.
    /// </summary>
    public RemotesDialog Dialog => ViewModel.Dialog;

    /// <summary>
    ///  Completes when the remotes and branches shown first are read, for tests.
    /// </summary>
    public Task RemotesLoaded => ViewModel.Loaded;

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(RemotesViewModel.Color):
                ColorButton.Flyout?.Hide();
                ShowColor();
                break;
            case nameof(RemotesViewModel.SelectedIsInactive):
                ShowToggleState();
                break;
        }
    }

    private void ShowColor()
        => ColorButton.Background = ViewModel.Color is { } html && Color.TryParse(html, out Color color)
            ? new SolidColorBrush(color)
            : null;

    private void ShowToggleState()
        => ToggleStateImage.Source = UpstreamImages.Named(ViewModel.SelectedIsInactive ? "EyeOpened" : "EyeClosed");

    private async Task<string?> PickFolderAsync()
    {
        IReadOnlyList<IStorageFolder> folders =
            await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { AllowMultiple = false });
        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }

    private async Task<string?> PickPuttyKeyAsync()
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = UpstreamTranslation.ByText(RemotesDialog.SshKeyOpenCaption),
            FileTypeFilter =
            [
                new FilePickerFileType(UpstreamTranslation.ByText(RemotesDialog.SshKeyOpenFilter))
                {
                    Patterns = ["*.ppk"]
                },
            ],
        });
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }
}
