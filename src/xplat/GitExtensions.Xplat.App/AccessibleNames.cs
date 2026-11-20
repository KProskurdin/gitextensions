using System.Globalization;
using System.Reflection;
using Avalonia.Data.Converters;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Diff;
using GitExtensions.Xplat.Core.Repository;

namespace GitExtensions.Xplat.App;

/// <summary>
///  What a screen reader says for a row of a list or tree. Upstream's WinForms rows and nodes are read by their text; an
///  Avalonia item without a name is read as its data's <c>ToString</c>, which for a record is all its fields. A commit is
///  read by its subject, author and date, a file by its path, a node by its text, and other rows by their text, display,
///  name or path; null keeps Avalonia's own name.
/// </summary>
public sealed class AccessibleNames : IValueConverter
{
    private static readonly string[] _textProperties = ["Text", "Display", "Name", "Path"];

    public static AccessibleNames Converter { get; } = new();

    public static string? Of(object? item) => item switch
    {
        null => null,
        string text => text,
        CommitListItem { Row: var row } => $"{row.Subject}, {row.Author}, {row.Date}",
        FileChange change => change.Path,
        CommitFile file => file.Path,
        RevisionDiffFile file => file.Path,
        FileTreeRow row => row.Text,
        DiffGroupHeader header => header.Text,
        RepoObjectNode node => node.Text,
        FileTreeNode node => node.Name,
        _ => TextOf(item),
    };

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => Of(value);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static string? TextOf(object item)
    {
        foreach (string name in _textProperties)
        {
            if (item.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance) is
                    { PropertyType: var type } property && type == typeof(string)
                && property.GetValue(item) is string { Length: > 0 } text)
            {
                return text;
            }
        }

        return null;
    }
}
