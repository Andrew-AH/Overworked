using System.Text.Json;

namespace Overworked.Core;

/// <summary>Shared by the app and the CLI: which folder holds the main repos, where the app lives, and the editor to open things in.</summary>
public static class Config
{
    static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Overworked", "config.json");

    // "editor", written by hand, needn't match the case the app writes.
    static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    sealed record Settings(string? Root = null, string? AppPath = null, string? Editor = null);

    public static string? LoadRoot() => Load().Root;

    public static void SaveRoot(string root) => Save(Load() with { Root = Path.GetFullPath(root) });

    /// <summary>The app's executable, recorded each time it starts so `ow app` can open it.</summary>
    public static string? LoadAppPath() => Load().AppPath;

    public static void SaveAppPath(string path)
    {
        var settings = Load();
        if (settings.AppPath != path) Save(settings with { AppPath = path });
    }

    /// <summary>
    /// The editor to open rows in, if config.json's "Editor" names one: the command on Windows ("cursor", "zed"), the
    /// app's name on macOS ("Cursor", "Zed", "Rider"). Null means VS Code.
    /// </summary>
    public static string? LoadEditor() => Load().Editor;

    static Settings Load() =>
        File.Exists(FilePath) ? JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath), Options) ?? new() : new();

    static void Save(Settings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(settings));
    }
}
