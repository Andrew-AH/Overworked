using System.Text.Json;

namespace Overworked.Core;

/// <summary>Shared by the app and the CLI: which folder holds the main repos, and where the app lives.</summary>
public static class Config
{
    static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Overworked", "config.json");

    sealed record Settings(string? Root = null, string? AppPath = null);

    public static string? LoadRoot() => Load().Root;

    public static void SaveRoot(string root) => Save(Load() with { Root = Path.GetFullPath(root) });

    /// <summary>The app's executable, recorded each time it starts so `ow app` can open it.</summary>
    public static string? LoadAppPath() => Load().AppPath;

    public static void SaveAppPath(string path)
    {
        var settings = Load();
        if (settings.AppPath != path) Save(settings with { AppPath = path });
    }

    static Settings Load() =>
        File.Exists(FilePath) ? JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new() : new();

    static void Save(Settings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(settings));
    }
}
