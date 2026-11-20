using CommunityToolkit.Mvvm.ComponentModel;

namespace GitExtensions.Xplat.Core.Settings;

/// <summary>
///  One font of the Fonts page: its family (or <see cref="DefaultChoice"/> for the app's own), size, bold and italic.
///  Only a font the user changed is stored; <see cref="DefaultChoice"/> removes the stored one.
/// </summary>
public sealed partial class FontChoice : ObservableObject
{
    /// <summary>
    ///  The text of a family list's first choice: no font stored, so the app's own applies.
    /// </summary>
    public const string DefaultChoice = "(default)";

    // Upstream's font dialog names the styles so.
    public const string BoldText = "Bold";
    public const string ItalicText = "Italic";

    /// <param name="installed">The installed families, in the order the list shows them.</param>
    /// <param name="stored">The stored font, or null for the app's own.</param>
    public FontChoice(AppFont font, string label, IReadOnlyList<string> installed, FontSetting? stored)
    {
        Font = font;
        Label = label;
        Stored = stored;

        // A stored family that is not installed here is listed too, so saving keeps it.
        List<string> families = [DefaultChoice, .. installed];
        if (stored is not null && !families.Contains(stored.Family))
        {
            families.Insert(1, stored.Family);
        }

        Families = families;
        Family = stored?.Family ?? DefaultChoice;
        Size = (decimal)(stored?.Size ?? DefaultSize(font));
        Bold = stored?.Bold == true;
        Italic = stored?.Italic == true;
    }

    public AppFont Font { get; }

    public string Label { get; }

    public FontSetting? Stored { get; }

    public IReadOnlyList<string> Families { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsChosen))]
    public partial string? Family { get; set; }

    [ObservableProperty]
    public partial decimal? Size { get; set; }

    [ObservableProperty]
    public partial bool Bold { get; set; }

    [ObservableProperty]
    public partial bool Italic { get; set; }

    /// <summary>
    ///  A family is chosen: the size and styles apply.
    /// </summary>
    public bool IsChosen => Family is { } family && family != DefaultChoice;

    /// <summary>
    ///  The font as chosen, or null for the app's own.
    /// </summary>
    public FontSetting? Chosen
        => IsChosen
            ? (Stored ?? new FontSetting(Family!, DefaultSize(Font))) with
            {
                Family = Family!,
                Size = (float)(Size ?? (decimal)DefaultSize(Font)),
                Bold = Bold,
                Italic = Italic,
            }
            : null;

    // Upstream's default sizes (its code font is 10 points, the others 9), for a family chosen where none was stored.
    public static float DefaultSize(AppFont font) => font == AppFont.Code ? 10 : 9;
}

/// <summary>
///  One question of the Confirmations page: checked means the app asks first. A three-state one shows upstream's
///  in-between state too (<see cref="Confirmations.CheckState"/>).
/// </summary>
public sealed partial class ConfirmationChoice(ConfirmationOption option, bool? checkState) : ObservableObject
{
    public Confirmation Confirmation => option.Confirmation;

    public string Label => option.Label;

    public bool IsThreeState => option.IsThreeState;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Asks))]
    public partial bool? CheckState { get; set; } = checkState;

    /// <summary>
    ///  True when the box is checked.
    /// </summary>
    public bool Asks
    {
        get => CheckState == true;
        set => CheckState = value;
    }
}

/// <summary>
///  A group of the Confirmations page, as upstream groups its questions.
/// </summary>
public sealed record ConfirmationGroup(string Header, IReadOnlyList<ConfirmationChoice> Choices);
