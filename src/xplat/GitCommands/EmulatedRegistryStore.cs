using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace GitCommands;

/// <summary>
///  File-backed values for platforms without the registry. Stored as one JSON object in the given directory.
/// </summary>
internal sealed class EmulatedRegistryStore
{
    private readonly string _directory;
    private readonly string _path;
    private readonly Lock _lock = new();

    public EmulatedRegistryStore(string directory)
    {
        _directory = directory;
        _path = Path.Combine(directory, "GitExtensions.registry.json");
    }

    public bool TryGet(string name, [NotNullWhen(true)] out string? value)
    {
        lock (_lock)
        {
            return Values.TryGetValue(name, out value);
        }
    }

    public void Set(string name, string value)
    {
        lock (_lock)
        {
            Values[name] = value;
            Save();
        }
    }

    public string[] GetNames()
    {
        lock (_lock)
        {
            return [.. Values.Keys];
        }
    }

    private Dictionary<string, string> Values
    {
        get => field ??= Load();
    }

    private Dictionary<string, string> Load()
    {
        if (!File.Exists(_path))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(_path)) ?? [];
        }
        catch (JsonException)
        {
            // Keep the unreadable file for inspection rather than overwriting it with an empty store.
            File.Move(_path, _path + ".corrupt", overwrite: true);
            return [];
        }
    }

    private void Save()
    {
        Directory.CreateDirectory(_directory);
        string tempPath = _path + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(Values));
        File.Move(tempPath, _path, overwrite: true);
    }
}
