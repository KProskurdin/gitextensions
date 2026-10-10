using System.Reflection;
using GitCommands;
using GitCommands.Git;
using GitCommands.Settings;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Extensibility.Settings;
using GitUI;

namespace GitExtensions.Xplat.Core.Plugins;

/// <summary>
///  The editor a plugin setting gets, by its upstream setting type.
/// </summary>
public enum PluginSettingKind
{
    /// <summary>A three-state check box (<see cref="BoolSetting"/>).</summary>
    Bool,

    /// <summary>A text box (<see cref="StringSetting"/>).</summary>
    Text,

    /// <summary>A masked text box (<see cref="PasswordSetting"/>).</summary>
    Password,

    /// <summary>A text box that accepts only a number of the setting's type (<see cref="NumberSetting{T}"/>).</summary>
    Number,

    /// <summary>A drop-down list (<see cref="ChoiceSetting"/>).</summary>
    Choice,

    /// <summary>Read-only text (<see cref="PseudoSetting"/>).</summary>
    Note,

    /// <summary>A link (a <see cref="PseudoSetting"/> with a link label); clicking it runs the plugin's handler.</summary>
    Link,

    /// <summary>A user name and password (<see cref="CredentialsSetting"/>), kept in the Windows Credential Manager.</summary>
    Credentials,

    /// <summary>A setting whose upstream editor is a custom WinForms control.</summary>
    Unsupported,
}

/// <summary>
///  One plugin setting in the Settings window, loaded and saved as upstream's setting control bindings do: at the effective
///  level the default value is shown, and a value equal to the effective one is not written.
/// </summary>
public sealed class PluginSettingRow : ObservableObject
{
    /// <summary>
    ///  Upstream's text for an explicitly empty string value (<c>StringSettingControlBinding.EmptyStringValue</c>).
    /// </summary>
    public const string EmptyStringValue = "<empty string>";

    /// <summary>
    ///  Upstream's placeholder of string settings (<c>DistributedSettingsPage</c>).
    /// </summary>
    public const string StringPlaceholder =
        "no value set; for empty string, enter \"" + EmptyStringValue + "\" without the double quotes";

    /// <summary>
    ///  Upstream's placeholder of number settings.
    /// </summary>
    public const string NumberPlaceholder = "no value set";

    private static readonly MethodInfo _createNumber =
        typeof(PluginSettingRow).GetMethod(nameof(CreateNumber), BindingFlags.NonPublic | BindingFlags.Static)!;

    private readonly Action<PluginSettingRow, SettingsSource> _load;
    private readonly Action<PluginSettingRow, SettingsSource> _save;
    private readonly Func<string?, bool> _isValid;
    private bool? _checked;
    private string? _text;

    private PluginSettingRow(
        PluginSettingKind kind,
        string caption,
        Action<PluginSettingRow, SettingsSource>? load = null,
        Action<PluginSettingRow, SettingsSource>? save = null,
        Func<string?, bool>? isValid = null)
    {
        Kind = kind;
        Caption = caption;
        _load = load ?? ((_, _) => { });
        _save = save ?? ((_, _) => { });
        _isValid = isValid ?? (_ => true);
    }

    public PluginSettingKind Kind { get; }

    public string Caption { get; }

    /// <summary>
    ///  The drop-down values of a <see cref="PluginSettingKind.Choice"/> setting.
    /// </summary>
    public IReadOnlyList<string> Choices { get; private init; } = [];

    /// <summary>
    ///  The hint shown in an empty text box, as upstream's.
    /// </summary>
    public string? Placeholder { get; private init; }

    /// <summary>
    ///  The value of a <see cref="PluginSettingKind.Bool"/> setting; null when it is not set at the edited level.
    /// </summary>
    public bool? Checked
    {
        get => _checked;
        set => SetProperty(ref _checked, value);
    }

    /// <summary>
    ///  The text of a text, password, number or note setting, or the chosen value of a choice setting.
    /// </summary>
    public string? Text
    {
        get => _text;
        set
        {
            if (SetProperty(ref _text, value))
            {
                RaisePropertyChanged(nameof(IsValid));
            }
        }
    }

    /// <summary>
    ///  False when a number setting's text is not a number of its type; upstream colors the text box red then.
    /// </summary>
    public bool IsValid => _isValid(_text);

    /// <summary>
    ///  The password of a <see cref="PluginSettingKind.Credentials"/> setting; <see cref="Text"/> is its user name.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    ///  False when the setting cannot be edited at the edited level or on this OS; <see cref="Text"/> says why for credentials.
    /// </summary>
    public bool IsEditable { get; private set; } = true;

    /// <summary>
    ///  Why <see cref="IsEditable"/> is false, or null.
    /// </summary>
    public string? NotEditableReason { get; private set; }

    /// <summary>
    ///  Credentials live in the Windows Credential Manager (upstream's <c>CredentialsManager</c>, through
    ///  AdysTech.CredentialManager), which other OSes do not have.
    /// </summary>
    public const string CredentialsNeedWindows =
        "Stored in the Windows Credential Manager, which this operating system does not have.";

    // Upstream's CredentialsSettingControlBinding: the user and the repository levels and the effective one have credentials.
    private static PluginSettingRow CreateCredentials(CredentialsSetting setting)
        => new(PluginSettingKind.Credentials, setting.Caption,
            (row, settings) =>
            {
                if (!OperatingSystem.IsWindows())
                {
                    row.IsEditable = false;
                    row.NotEditableReason = CredentialsNeedWindows;
                    return;
                }

                row.IsEditable =
                    settings.SettingLevel is SettingLevel.Global or SettingLevel.Local or SettingLevel.Effective;
                System.Net.NetworkCredential credentials = row.IsEditable
                    ? setting.GetValueOrDefault(settings)
                    : new System.Net.NetworkCredential();
                row.Text = credentials.UserName;
                row.Password = credentials.Password;
            },
            (row, settings) =>
            {
                if (row.IsEditable)
                {
                    setting.SaveValue(settings, row.Text ?? "", row.Password ?? "");
                    setting.Save();
                }
            });

    /// <summary>
    ///  The row for <paramref name="setting"/>; a setting with its own WinForms editor gets an unsupported row.
    /// </summary>
    public static PluginSettingRow Create(ISetting setting)
    {
        if (setting.CreateControlBinding() is not null)
        {
            return new PluginSettingRow(PluginSettingKind.Unsupported, setting.Caption);
        }

        return setting switch
        {
            BoolSetting bools => new PluginSettingRow(PluginSettingKind.Bool, bools.Caption,
                (row, settings) =>
                    row.Checked = IsEffective(settings) ? bools.ValueOrDefault(settings) : bools[settings],
                (row, settings) =>
                {
                    if (!IsEffective(settings) || bools.ValueOrDefault(settings) != row.Checked)
                    {
                        bools[settings] = row.Checked;
                    }
                }),
            StringSetting strings => CreateText(PluginSettingKind.Text, strings.Caption,
                settings => IsEffective(settings) ? strings.ValueOrDefault(settings) : strings[settings],
                (settings, value) => strings[settings] = value, strings.ValueOrDefault),
            PasswordSetting passwords => CreateText(PluginSettingKind.Password, passwords.Caption,
                settings => IsEffective(settings) ? passwords.ValueOrDefault(settings) : passwords[settings],
                (settings, value) => passwords[settings] = value, passwords.ValueOrDefault),
            ChoiceSetting choices => new PluginSettingRow(PluginSettingKind.Choice, choices.Caption,
                (row, settings) =>
                    row.Text = IsEffective(settings) ? choices.ValueOrDefault(settings) : choices[settings],
                (row, settings) =>
                {
                    // As upstream's drop-down list: a stored value that is not one of the choices is kept unless changed.
                    string? value = row.Text is { } text && choices.Values.Contains(text) ? text : null;
                    if (!IsEffective(settings) || choices.ValueOrDefault(settings) != value)
                    {
                        choices[settings] = value;
                    }
                }) { Choices = [.. choices.Values], },
            CredentialsSetting credentials => CreateCredentials(credentials),
            PseudoSetting { CustomControl: System.Windows.Forms.TextBox note } pseudo =>
                new PluginSettingRow(PluginSettingKind.Note, pseudo.Caption.Trim()) { _text = note.Text },
            PseudoSetting { CustomControl: System.Windows.Forms.LinkLabel link } pseudo =>
                new PluginSettingRow(PluginSettingKind.Link, pseudo.Caption.Trim())
                {
                    _text = link.Text, Click = link.RaiseClick
                },
            _ when setting.GetType() is { IsGenericType: true } type &&
                   type.GetGenericTypeDefinition() == typeof(NumberSetting<>) =>
                (PluginSettingRow)_createNumber.MakeGenericMethod(type.GetGenericArguments()[0])
                    .Invoke(null, [setting])!,
            _ => new PluginSettingRow(PluginSettingKind.Unsupported, setting.Caption),
        };
    }

    /// <summary>
    ///  What clicking a <see cref="PluginSettingKind.Link"/> does.
    /// </summary>
    public Action? Click { get; private init; }

    internal void Load(SettingsSource settings) => _load(this, settings);

    internal void Save(SettingsSource settings) => _save(this, settings);

    private static bool IsEffective(SettingsSource settings) => settings.SettingLevel == SettingLevel.Effective;

    // Upstream's StringSettingControlBinding and PasswordSettingControlBinding: an empty value is shown as "<empty string>",
    // the text is trimmed, and an empty text box removes the value.
    private static PluginSettingRow CreateText(PluginSettingKind kind, string caption,
        Func<SettingsSource, string?> read, Action<SettingsSource, string?> write,
        Func<SettingsSource, string> effectiveValue)
        => new(kind, caption,
            (row, settings) => row.Text = read(settings) is { Length: 0 } ? EmptyStringValue : read(settings),
            (row, settings) =>
            {
                string? value = row.Text?.Trim() ?? "";
                row.Text = value;
                value = value switch
                {
                    "" => null,
                    EmptyStringValue => "",
                    _ => value,
                };

                if (!IsEffective(settings) || effectiveValue(settings) != value)
                {
                    write(settings, value);
                }
            }) { Placeholder = StringPlaceholder, };

    // Upstream's NumberSettingTextBoxBinding: an empty or invalid number removes the value.
    private static PluginSettingRow CreateNumber<T>(NumberSetting<T> setting)
        => new(PluginSettingKind.Number, setting.Caption,
            (row, settings) =>
                row.Text = (IsEffective(settings) ? setting.ValueOrDefault(settings) : setting[settings])?.ToString() ??
                           "",
            (row, settings) =>
            {
                if (string.IsNullOrEmpty(row.Text) || TryParse<T>(row.Text) is not { } value)
                {
                    setting[settings] = null;
                    return;
                }

                if (!IsEffective(settings) || setting.ValueOrDefault(settings)?.ToString() != row.Text)
                {
                    setting[settings] = value;
                }
            },
            text => string.IsNullOrEmpty(text) || TryParse<T>(text) is not null) { Placeholder = NumberPlaceholder, };

    // The types upstream's NumberSetting<T>.TryConvertFromString accepts (it is internal).
    private static object? TryParse<T>(string text)
    {
        Type type = typeof(T);
        if (type == typeof(int) && int.TryParse(text, out int intValue))
        {
            return intValue;
        }

        if (type == typeof(float) && float.TryParse(text, out float floatValue))
        {
            return floatValue;
        }

        if (type == typeof(double) && double.TryParse(text, out double doubleValue))
        {
            return doubleValue;
        }

        return type == typeof(long) && long.TryParse(text, out long longValue) ? longValue : null;
    }
}

/// <summary>
///  The settings of one plugin, as upstream's <c>PluginSettingsPage</c> edits them: the rows of its settings, read and
///  written under the plugin's keys in the given settings.
/// </summary>
public sealed class PluginSettingsEditor
{
    private readonly GitPluginSettingsContainer _container;

    public PluginSettingsEditor(IGitPlugin plugin, SettingsSource settings)
    {
        Plugin = plugin;

        // As upstream: a container of its own, so editing does not change the source the registered plugin reads.
        _container = new GitPluginSettingsContainer(plugin.Id, plugin.Description ?? "");
        _container.SetSettingsSource(settings);
        Rows = plugin.HasSettings ? [.. plugin.GetSettings().Select(PluginSettingRow.Create)] : [];
        foreach (PluginSettingRow row in Rows)
        {
            row.Load(_container);
        }
    }

    public IGitPlugin Plugin { get; }

    public string Title => Plugin.Name ?? "";

    public IReadOnlyList<PluginSettingRow> Rows { get; }

    public void Save()
    {
        foreach (PluginSettingRow row in Rows)
        {
            row.Save(_container);
        }
    }
}

/// <summary>
///  Where plugin settings are edited and stored.
/// </summary>
public interface IPluginSettingsStore
{
    /// <summary>
    ///  The settings the Settings window edits at <paramref name="level"/>, upstream's "Settings source": the repository's
    ///  effective settings by default, as upstream's settings dialog shows them; without a repository, the user's global
    ///  settings whatever the level.
    /// </summary>
    SettingsSource Open(string? repositoryPath, SettingLevel level = SettingLevel.Effective);

    /// <summary>
    ///  Writes the settings <see cref="Open"/> returned to disk.
    /// </summary>
    void Save(SettingsSource settings);
}

/// <summary>
///  The GitExtensions.settings files upstream uses: the user's, the repository's shared one and its .git one.
/// </summary>
public sealed class UpstreamPluginSettingsStore : IPluginSettingsStore
{
    public SettingsSource Open(string? repositoryPath, SettingLevel level = SettingLevel.Effective)
    {
        if (string.IsNullOrEmpty(repositoryPath))
        {
            return DistributedSettings.CreateGlobal();
        }

        GitModule module = new(new GitExecutorProvider(new GitDirectoryResolver()), repositoryPath);
        return level switch
        {
            SettingLevel.Local => DistributedSettings.CreateLocal(module),
            SettingLevel.Distributed => DistributedSettings.CreateDistributed(module),
            SettingLevel.Global => DistributedSettings.CreateGlobal(),
            _ => DistributedSettings.CreateEffective(module),
        };
    }

    public void Save(SettingsSource settings)
    {
        if (settings is DistributedSettings distributed)
        {
            distributed.Save();
        }
    }
}

/// <summary>
///  Settings values in memory, at one level. Tests use it so they never write the user's settings files.
/// </summary>
public sealed class InMemorySettingsSource(SettingLevel level = SettingLevel.Global) : SettingsSource
{
    private readonly Dictionary<string, string> _values = [];

    public override SettingLevel SettingLevel => level;

    public IReadOnlyDictionary<string, string> Values => _values;

    public override string? GetValue(string name) => _values.GetValueOrDefault(name);

    public override void SetValue(string name, string? value)
    {
        if (value is null)
        {
            _values.Remove(name);
        }
        else
        {
            _values[name] = value;
        }
    }
}

/// <summary>
///  One settings source for every repository. Tests use it with an <see cref="InMemorySettingsSource"/>.
/// </summary>
public sealed class InMemoryPluginSettingsStore(SettingsSource settings) : IPluginSettingsStore
{
    public int SaveCount { get; private set; }

    private readonly Dictionary<SettingLevel, InMemorySettingsSource> _levels = [];

    // The given source is the effective and the global one; each repository level has a source of its own.
    public SettingsSource Open(string? repositoryPath, SettingLevel level = SettingLevel.Effective)
    {
        if (repositoryPath is null || level is SettingLevel.Effective or SettingLevel.Global)
        {
            return settings;
        }

        if (!_levels.TryGetValue(level, out InMemorySettingsSource? source))
        {
            _levels[level] = source = new InMemorySettingsSource(level);
        }

        return source;
    }

    public void Save(SettingsSource saved) => SaveCount++;
}
