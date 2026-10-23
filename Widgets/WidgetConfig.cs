using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace WidgetTools;

/// <summary>
/// Which widgets are enabled, persisted to %AppData%\DesktopWidgets\config.json.
/// </summary>
public sealed class WidgetConfig
{
    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DesktopWidgets");
    private static readonly string FilePath = Path.Combine(Dir, "config.json");

    public Dictionary<string, bool> Enabled { get; set; } = new();

    public bool IsEnabled(string id) => Enabled.TryGetValue(id, out var v) && v;

    public void Set(string id, bool on) => Enabled[id] = on;

    public static WidgetConfig Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var cfg = JsonSerializer.Deserialize<WidgetConfig>(json);
                if (cfg is not null) return cfg;
            }
        }
        catch { /* corrupt / unreadable — fall through to defaults */ }

        // First run: show the music widget so there's something on screen.
        var defaults = new WidgetConfig();
        defaults.Set("music", true);
        return defaults;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
        catch { /* best effort; a failed save just forgets the toggle */ }
    }
}
