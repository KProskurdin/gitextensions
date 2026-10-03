using System.Globalization;
using Microsoft.Win32;

namespace GitCommands;

/// <summary>
///  Stands in for the <c>HKCU\Software\GitExtensions</c> registry key that <see cref="AppSettings"/> uses for a few machine-level
///  values (git command, ssh path, install dir, ...). On Windows it wraps the real key so behavior is unchanged; elsewhere it
///  keeps the values in <see cref="EmulatedRegistryStore"/>, in the same per-user directory as the settings file.
/// </summary>
internal sealed class XplatRegistryKey
{
    private static readonly Lazy<EmulatedRegistryStore> _emulatedStore = new(() => new EmulatedRegistryStore(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GitExtensions")));

    private readonly RegistryKey? _key;

    private XplatRegistryKey(RegistryKey? key)
    {
        _key = key;
    }

    internal static XplatRegistryKey OpenVersionIndependentKey()
    {
        if (OperatingSystem.IsWindows())
        {
            RegistryKey key =
                Registry.CurrentUser.CreateSubKey("Software\\GitExtensions",
                    RegistryKeyPermissionCheck.ReadWriteSubTree)
                ?? throw new InvalidOperationException("Cannot open the GitExtensions registry key.");
            return new XplatRegistryKey(key);
        }

        return new XplatRegistryKey(key: null);
    }

    public object? GetValue(string name) => GetValue(name, defaultValue: null);

    public object? GetValue(string name, object? defaultValue)
    {
        if (_key is not null && OperatingSystem.IsWindows())
        {
            return _key.GetValue(name, defaultValue);
        }

        return _emulatedStore.Value.TryGet(name, out string? value) ? value : defaultValue;
    }

    public void SetValue(string name, object value)
    {
        if (_key is not null && OperatingSystem.IsWindows())
        {
            _key.SetValue(name, value);
            return;
        }

        _emulatedStore.Value.Set(name, Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
    }

    public XplatRegistryKey? OpenSubKey(string name)
    {
        if (_key is not null && OperatingSystem.IsWindows())
        {
            RegistryKey? subKey = _key.OpenSubKey(name);
            return subKey is null ? null : new XplatRegistryKey(subKey);
        }

        // There is no legacy registry data to import elsewhere.
        return null;
    }

    public string[] GetValueNames()
    {
        if (_key is not null && OperatingSystem.IsWindows())
        {
            return _key.GetValueNames();
        }

        return _emulatedStore.Value.GetNames();
    }
}
