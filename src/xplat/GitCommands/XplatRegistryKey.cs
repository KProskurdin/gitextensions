using System.Collections.Concurrent;
using Microsoft.Win32;

namespace GitCommands;

/// <summary>
///  Stands in for the <c>HKCU\Software\GitExtensions</c> registry key that <see cref="AppSettings"/> uses for a few machine-level
///  values (git command, ssh path, install dir, ...). On Windows it wraps the real key so behavior is unchanged; elsewhere it
///  keeps the values in memory.
/// </summary>
/// <remarks>
///  TODO(Phase 2): persist the emulated values, e.g. below the XDG config directory, once the settings location is decided.
/// </remarks>
internal sealed class XplatRegistryKey
{
    private static readonly ConcurrentDictionary<string, object> _emulatedValues = new();

    private readonly RegistryKey? _key;

    private XplatRegistryKey(RegistryKey? key)
    {
        _key = key;
    }

    internal static XplatRegistryKey OpenVersionIndependentKey()
    {
        if (OperatingSystem.IsWindows())
        {
            RegistryKey key = Registry.CurrentUser.CreateSubKey("Software\\GitExtensions", RegistryKeyPermissionCheck.ReadWriteSubTree)
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

        return _emulatedValues.TryGetValue(name, out object? value) ? value : defaultValue;
    }

    public void SetValue(string name, object value)
    {
        if (_key is not null && OperatingSystem.IsWindows())
        {
            _key.SetValue(name, value);
            return;
        }

        _emulatedValues[name] = value;
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

        return [.. _emulatedValues.Keys];
    }
}
