using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using GitExtensions.Xplat.Core.Avatars;

namespace GitExtensions.Xplat.App;

/// <summary>
///  An author's avatar, as upstream's avatar controls show it: the author's initials on a color from upstream's palette at
///  once, replaced by the image the avatar settings find (<see cref="AppServices.Avatars"/>) when there is one.
/// </summary>
public sealed class AvatarView : Border
{
    public static readonly StyledProperty<string?> EmailProperty =
        AvaloniaProperty.Register<AvatarView, string?>(nameof(Email));

    public static readonly StyledProperty<string?> AuthorNameProperty =
        AvaloniaProperty.Register<AvatarView, string?>(nameof(AuthorName));

    /// <summary>
    ///  The size of the image asked of the providers, in pixels (upstream asks 80 for the commit info).
    /// </summary>
    public static readonly StyledProperty<int> ImageSizeProperty =
        AvaloniaProperty.Register<AvatarView, int>(nameof(ImageSize), 80);

    // Upstream draws the initials to fill the image; this share of the height looks the same.
    private const double InitialsFontShare = 0.42;

    private readonly TextBlock _initials = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        FontWeight = FontWeight.SemiBold,
    };

    private readonly Image _image = new() { Stretch = Stretch.UniformToFill };
    private int _requests;

    public AvatarView()
    {
        ClipToBounds = true;
        Child = new Panel { Children = { _initials, _image } };
    }

    public string? Email
    {
        get => GetValue(EmailProperty);
        set => SetValue(EmailProperty, value);
    }

    public string? AuthorName
    {
        get => GetValue(AuthorNameProperty);
        set => SetValue(AuthorNameProperty, value);
    }

    public int ImageSize
    {
        get => GetValue(ImageSizeProperty);
        set => SetValue(ImageSizeProperty, value);
    }

    /// <summary>
    ///  The initials on show, for tests.
    /// </summary>
    public string InitialsText => _initials.Text ?? "";

    /// <summary>
    ///  Whether an image replaced the initials.
    /// </summary>
    public bool HasImage => _image.Source is not null;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == EmailProperty || change.Property == AuthorNameProperty
                                             || change.Property == ImageSizeProperty)
        {
            Refresh();
        }
        else if (change.Property == BoundsProperty)
        {
            _initials.FontSize = Math.Max(6, Bounds.Height * InitialsFontShare);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        AppServices.Avatars.AvatarsChanged += OnAvatarsChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        AppServices.Avatars.AvatarsChanged -= OnAvatarsChanged;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnAvatarsChanged(object? sender, EventArgs e) => Dispatcher.UIThread.Post(Refresh);

    /// <summary>
    ///  Reads the avatar again, after the avatar settings changed.
    /// </summary>
    public void Refresh()
    {
        int request = ++_requests;
        _image.Source = null;
        string? email = Email;
        string? name = AuthorName;
        if (string.IsNullOrEmpty(email) && string.IsNullOrEmpty(name))
        {
            _initials.Text = "";
            Background = null;
            return;
        }

        (string initials, System.Drawing.Color background, System.Drawing.Color foreground) =
            AppServices.Preferences.AuthorInitials.For(email ?? "", name);
        _initials.Text = initials;
        _initials.Foreground = new SolidColorBrush(Color.FromArgb(foreground.A, foreground.R, foreground.G, foreground.B));
        Background = new SolidColorBrush(Color.FromArgb(background.A, background.R, background.G, background.B));
        if (!string.IsNullOrEmpty(email))
        {
            _ = LoadImageAsync(request, email, name);
        }
    }

    private async Task LoadImageAsync(int request, string email, string? name)
    {
        byte[]? data;
        try
        {
            data = await AppServices.Avatars.GetImageAsync(email, name, ImageSize);
        }
        catch (Exception ex) when (ex is IOException or HttpRequestException or InvalidOperationException)
        {
            return;
        }

        if (data is null || request != _requests)
        {
            return;
        }

        try
        {
            using MemoryStream stream = new(data);
            _image.Source = new Bitmap(stream);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            // Not an image Avalonia can decode: the initials stay, as upstream's safety net shows its fallback.
        }
    }
}
