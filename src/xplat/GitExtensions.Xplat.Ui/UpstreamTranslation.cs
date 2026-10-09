using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using GitCommands;
using GitExtensions.Extensibility.Translations;
using GitExtensions.Extensibility.Translations.Xliff;

namespace GitExtensions.Xplat.Ui;

/// <summary>
///  Upstream's translations for the windows that reimplement upstream forms: the Transifex <c>.xlf</c> files in the
///  Translation folder next to the app, in the language of upstream's <c>translation</c> setting
///  (<see cref="AppSettings.CurrentTranslation"/>), read by upstream's <see cref="Translator"/>. Upstream translates a form
///  by its type name (the category) and its fields' names; a window that reimplements the form names its controls as the
///  form's fields, so the same entries apply. A text without a translation keeps the window's English text.
/// </summary>
public static class UpstreamTranslation
{
    private const string TextProperty = "Text";
    private const string HeaderTextProperty = "HeaderText";
    private const string ToolTipTextProperty = "ToolTipText";
    private const string FormItemName = "$this";

    /// <summary>
    ///  The translation of upstream's text <paramref name="name"/> (a <c>TranslationString</c> field such as
    ///  <c>_loading</c>, or a control) in <paramref name="category"/> (the upstream form or class), or
    ///  <paramref name="english"/>.
    /// </summary>
    public static string Text(string category, string name, string english)
        => Find(category, name, TextProperty) ?? english;

    /// <summary>
    ///  The translated value of <paramref name="name"/>.<paramref name="property"/> in <paramref name="category"/>, or null
    ///  when the current language has none.
    /// </summary>
    public static string? Find(string category, string name, string property)
    {
        string? language = AppSettings.CurrentTranslation;
        if (string.IsNullOrEmpty(language))
        {
            return null;
        }

        foreach (TranslationFile file in Translator.GetTranslation(language).Values)
        {
            TranslationItem? item = file.GetTranslationCategory(category)?.Body.GetTranslationItem(name, property);
            if (!string.IsNullOrEmpty(item?.Value))
            {
                return item.Value;
            }
        }

        return null;
    }

    /// <summary>
    ///  Translates <paramref name="root"/> as upstream translates the form <paramref name="category"/>: the window's title
    ///  (upstream's <c>$this</c>), and every named control, menu item and context menu item under it by its name: the text
    ///  of a text block, the content of a button, check box or label, the header of a tab, group or menu item, and the
    ///  tooltip. A column header is named as upstream's grid column, whose text is its <c>HeaderText</c>.
    /// </summary>
    public static void Apply(StyledElement root, string category)
    {
        if (string.IsNullOrEmpty(AppSettings.CurrentTranslation))
        {
            return;
        }

        if (root is Window window && Find(category, FormItemName, TextProperty) is { } title)
        {
            window.Title = WithoutMnemonic(title);
        }

        foreach (StyledElement element in Elements(root))
        {
            if (string.IsNullOrEmpty(element.Name))
            {
                continue;
            }

            if ((Find(category, element.Name, TextProperty) ?? Find(category, element.Name, HeaderTextProperty)) is { } text)
            {
                SetText(element, text);
            }

            if (element is Control control && Find(category, element.Name, ToolTipTextProperty) is { } toolTip)
            {
                ToolTip.SetTip(control, toolTip);
            }
        }
    }

    /// <summary>
    ///  Upstream's WinForms text with its mnemonic ("&amp;Open") as an Avalonia access key ("_Open").
    /// </summary>
    public static string WithAccessKey(string text)
        => ConvertMnemonic(text, accessKey: true);

    /// <summary>
    ///  Upstream's WinForms text without its mnemonic, for text that shows no access key.
    /// </summary>
    public static string WithoutMnemonic(string text)
        => ConvertMnemonic(text, accessKey: false);

    private static IEnumerable<StyledElement> Elements(StyledElement root)
    {
        Stack<StyledElement> pending = new([root]);
        while (pending.Count > 0)
        {
            StyledElement element = pending.Pop();
            yield return element;
            foreach (ILogical child in element.GetLogicalChildren())
            {
                if (child is StyledElement styled)
                {
                    pending.Push(styled);
                }
            }

            // Context menus and flyouts are not in the logical tree until they open.
            if (element is Control { ContextMenu: { } contextMenu })
            {
                pending.Push(contextMenu);
            }

            if (element is Control control && FlyoutBase.GetAttachedFlyout(control) is MenuFlyout attached)
            {
                PushMenuItems(pending, attached.Items);
            }

            if (element is Button { Flyout: MenuFlyout flyout })
            {
                PushMenuItems(pending, flyout.Items);
            }
        }

        static void PushMenuItems(Stack<StyledElement> pending, IEnumerable<object?> items)
        {
            foreach (object? item in items)
            {
                if (item is StyledElement styled)
                {
                    pending.Push(styled);
                }
            }
        }
    }

    private static void SetText(StyledElement element, string text)
    {
        switch (element)
        {
            case TextBlock textBlock:
                textBlock.Text = WithoutMnemonic(text);
                break;
            case HeaderedContentControl headered:
                headered.Header = WithAccessKey(text);
                break;
            case HeaderedItemsControl headeredItems:
                headeredItems.Header = WithAccessKey(text);
                break;
            case ContentControl content when content.Content is null or string:
                content.Content = WithAccessKey(text);
                break;
        }
    }

    private static string ConvertMnemonic(string text, bool accessKey)
    {
        System.Text.StringBuilder result = new(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '&')
            {
                if (i + 1 < text.Length && text[i + 1] == '&')
                {
                    result.Append('&');
                    i++;
                }
                else if (accessKey)
                {
                    result.Append('_');
                }
            }
            else if (c == '_' && accessKey)
            {
                result.Append("__");
            }
            else
            {
                result.Append(c);
            }
        }

        return result.ToString();
    }
}
