using System.Collections;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>ComboBoxExtensions.ResizeDropDownWidth</c>: a drop-down as wide as its longest item and a scroll bar, at
///  least <see cref="MinWidth"/> and at most <see cref="MaxWidth"/>, while the box keeps its own width. Avalonia's drop-down
///  is as wide as its box, so the width is set on the popup's content when it opens.
/// </summary>
public static class DropDownWidth
{
    /// <summary>
    ///  Upstream's <c>BranchDropDownMinWidth</c>.
    /// </summary>
    public const double MinWidth = 200;

    /// <summary>
    ///  Upstream's <c>BranchDropDownMaxWidth</c>.
    /// </summary>
    public const double MaxWidth = 600;

    // Upstream adds the vertical scroll bar's width (17 pixels at 100 %); the list items here also have Fluent's padding,
    // which WinForms's do not.
    private const double ScrollBarWidth = 17;
    private const double ItemPadding = 24;

    /// <summary>
    ///  Fits the drop-down of <paramref name="box"/> (an <see cref="AutoCompleteBox"/> or <see cref="ComboBox"/>) to its
    ///  items each time it opens.
    /// </summary>
    public static void FitToItems(TemplatedControl box)
    {
        Popup? popup = null;
        box.TemplateApplied += (_, e) => popup = e.NameScope.Find<Popup>("PART_Popup");
        switch (box)
        {
            // As upstream, the matches the list shows, not every item, measured again as the typed text changes them.
            case AutoCompleteBox autoComplete:
                autoComplete.DropDownOpened += (_, _) => Fit(popup, box, Matches(autoComplete));
                autoComplete.TextChanged += (_, _) =>
                {
                    if (autoComplete.IsDropDownOpen)
                    {
                        Fit(popup, box, Matches(autoComplete));
                    }
                };
                break;
            case ComboBox comboBox:
                comboBox.DropDownOpened += (_, _) => Fit(popup, box, comboBox.ItemsSource ?? comboBox.Items);
                break;
        }
    }

    /// <summary>
    ///  The drop-down width for <paramref name="items"/> in the font of <paramref name="box"/>.
    /// </summary>
    public static double For(TemplatedControl box, IEnumerable? items)
    {
        Typeface typeface = new(box.FontFamily, box.FontStyle, box.FontWeight);
        double widest = 0;
        foreach (object? item in items ?? Array.Empty<object>())
        {
            FormattedText text = new(item?.ToString() ?? "", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface,
                box.FontSize, Brushes.Black);
            widest = Math.Max(widest, text.Width);
        }

        return Math.Clamp(widest + ItemPadding + ScrollBarWidth, MinWidth, MaxWidth);
    }

    // The items the box's own filter keeps for its text.
    private static IEnumerable<object?> Matches(AutoCompleteBox box)
    {
        string text = box.Text ?? "";
        IEnumerable<object?> items = box.ItemsSource?.Cast<object?>() ?? [];
        return box.ItemFilter is { } itemFilter ? items.Where(item => itemFilter(text, item))
            : box.TextFilter is { } textFilter ? items.Where(item => textFilter(text, item?.ToString()))
            : items;
    }

    private static void Fit(Popup? popup, TemplatedControl box, IEnumerable? items)
    {
        if (popup?.Child is Control content)
        {
            content.Width = For(box, items);
        }
    }
}
