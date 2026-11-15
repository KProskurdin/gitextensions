using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using GitExtensions.Xplat.Core.Plugins;

namespace GitExtensions.Xplat.App;

/// <summary>
///  The editor of a plugin setting row on the plugins settings page, by the row's kind, as upstream's
///  SettingControlBindingsProvider chooses them; the row (in GitExtensions.Xplat.Core) holds the value. A row makes only its
///  own editor, so a page lists one control per setting.
/// </summary>
public sealed class PluginSettingRowTemplate : IDataTemplate
{
    public Control? Build(object? param) => param is PluginSettingRow row ? CreateSettingControl(row) : null;

    public bool Match(object? data) => data is PluginSettingRow;

    // One editor per upstream setting type, as upstream's SettingControlBindingsProvider chooses them.
    private static Control CreateSettingControl(PluginSettingRow row)
    {
        switch (row.Kind)
        {
            case PluginSettingKind.Bool:
                CheckBox check = new() { Content = row.Caption, IsThreeState = true, IsChecked = row.Checked };
                check.IsCheckedChanged += (_, _) => row.Checked = check.IsChecked;
                return check;
            case PluginSettingKind.Choice:
                ComboBox choice = new() { ItemsSource = row.Choices, SelectedItem = row.Text, MinWidth = 200 };
                choice.SelectionChanged += (_, _) => row.Text = choice.SelectedItem as string;
                return Labeled(row.Caption, choice);
            case PluginSettingKind.Text or PluginSettingKind.Password or PluginSettingKind.Number:
                TextBox text = new() { Text = row.Text, PlaceholderText = row.Placeholder };
                if (row.Kind == PluginSettingKind.Password)
                {
                    text.PasswordChar = '●';
                }

                // Not TextChanged: it is raised later, so OK pressed right after typing would save the old text.
                text.PropertyChanged += (_, e) =>
                {
                    if (e.Property != TextBox.TextProperty)
                    {
                        return;
                    }

                    row.Text = text.Text;

                    // Upstream colors a number box red while its text is not a number.
                    text.Classes.Set("errorText", !row.IsValid);
                };
                return Labeled(row.Caption, text);
            case PluginSettingKind.Credentials:
                // Upstream's CredentialsControl: a user name and a password.
                if (!row.IsEditable)
                {
                    return Labeled(row.Caption, new TextBlock
                    {
                        Text = row.NotEditableReason ?? "Not available at this settings source.",
                        Opacity = 0.7,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        VerticalAlignment = VerticalAlignment.Center,
                    });
                }

                TextBox user = new() { Text = row.Text, PlaceholderText = "User name" };
                TextBox password = new() { Text = row.Password, PasswordChar = '●', PlaceholderText = "Password" };
                user.PropertyChanged += (_, e) =>
                {
                    if (e.Property == TextBox.TextProperty)
                    {
                        row.Text = user.Text;
                    }
                };
                password.PropertyChanged += (_, e) =>
                {
                    if (e.Property == TextBox.TextProperty)
                    {
                        row.Password = password.Text;
                    }
                };
                return Labeled(row.Caption, new StackPanel { Spacing = 4, Children = { user, password } });
            case PluginSettingKind.Link:
                HyperlinkButton link = new() { Content = row.Text };
                link.Click += (_, _) => row.Click?.Invoke();
                return link;
            case PluginSettingKind.Note:
                return new TextBlock
                {
                    Text = row.Text, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Opacity = 0.8
                };
            default:
                return Labeled(row.Caption,
                    new TextBlock
                    {
                        Text = "This setting cannot be edited in this version yet.",
                        Opacity = 0.7,
                        VerticalAlignment = VerticalAlignment.Center,
                    });
        }
    }

    private static Control Labeled(string caption, Control editor)
    {
        Grid grid = new() { ColumnDefinitions = new ColumnDefinitions("220,*") };
        TextBlock label = new()
        {
            Text = caption,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Avalonia.Thickness(0, 0, 8, 0),
        };
        Grid.SetColumn(editor, 1);
        grid.Children.Add(label);
        grid.Children.Add(editor);
        return grid;
    }
}
