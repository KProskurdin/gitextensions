using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>HelpImageDisplayUserControl</c>: a dialog's help picture beside its options, with "Hide help" above it, or
///  only the information button that shows it again; which of the two is remembered per dialog. A second picture can show
///  while the pointer is on the picture, with a note saying so.
/// </summary>
public sealed class HelpImageView : StackPanel
{
    private const string HideHelpText = "Hide help";
    private const string ShowHelpTip = "Show help";

    private readonly Image _picture = new() { Stretch = Stretch.None, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly TextBlock _hoverText = new() { TextWrapping = TextWrapping.Wrap, MaxWidth = 150 };
    private readonly HyperlinkButton _hide = new() { Content = HideHelpText, Padding = new Thickness(0) };
    private readonly Button _show = new()
    {
        Content = new Image { Source = UpstreamImages.Named("Information"), Width = 16, Height = 16 },
        HorizontalAlignment = HorizontalAlignment.Left,
    };

    private IAppPreferences? _preferences;
    private string _dialog = "";
    private Bitmap? _image1;
    private Bitmap? _image2;
    private bool _isOnHoverShowImage2;
    private bool _isHovering;

    public HelpImageView()
    {
        Spacing = 4;
        ToolTip.SetTip(_show, ShowHelpTip);
        Children.Add(_show);
        Children.Add(_hide);
        Children.Add(_hoverText);
        Children.Add(_picture);
        _hide.Click += (_, _) => SetExpanded(false);
        _show.Click += (_, _) => SetExpanded(true);
        _picture.PointerEntered += (_, _) => Hover(true);
        _picture.PointerExited += (_, _) => Hover(false);
        ShowState();
    }

    /// <summary>
    ///  The note shown while <see cref="IsOnHoverShowImage2"/> is on.
    /// </summary>
    public string NoticeText
    {
        get => _hoverText.Text ?? "";
        set => _hoverText.Text = value;
    }

    public Bitmap? Image1
    {
        get => _image1;
        set
        {
            _image1 = value;
            ShowState();
        }
    }

    public Bitmap? Image2
    {
        get => _image2;
        set
        {
            _image2 = value;
            ShowState();
        }
    }

    public bool IsOnHoverShowImage2
    {
        get => _isOnHoverShowImage2;
        set
        {
            _isOnHoverShowImage2 = value;
            ShowState();
        }
    }

    public bool IsExpanded { get; private set; } = true;

    /// <summary>
    ///  Reads whether the picture shows for <paramref name="dialog"/> (upstream's <c>UniqueIsExpandedSettingsId</c>); the
    ///  whole view is hidden while upstream's <c>DontShowHelpImages</c> is on.
    /// </summary>
    public void Load(IAppPreferences preferences, string dialog)
    {
        _preferences = preferences;
        _dialog = dialog;
        IsVisible = !preferences.DontShowHelpImages;
        IsExpanded = preferences.IsHelpExpanded(dialog);
        ShowState();
    }

    private void SetExpanded(bool expanded)
    {
        IsExpanded = expanded;
        _preferences?.SetHelpExpanded(_dialog, expanded);
        ShowState();
    }

    private void Hover(bool hovering)
    {
        _isHovering = hovering && _isOnHoverShowImage2;
        ShowState();
    }

    private void ShowState()
    {
        _show.IsVisible = !IsExpanded;
        _hide.IsVisible = IsExpanded;
        _picture.IsVisible = IsExpanded;
        _hoverText.IsVisible = IsExpanded && _isOnHoverShowImage2;
        _picture.Source = _isHovering && _image2 is not null ? _image2 : _image1;
    }
}
