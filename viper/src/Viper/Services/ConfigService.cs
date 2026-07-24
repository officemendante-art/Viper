using System;
using System.IO;
using System.Text.Json;
using Viper.Models;

namespace Viper.Services;

/// <summary>
/// Loads and saves <see cref="ViperConfig"/> to %ProgramData%\Viper\config.json.
/// Uses atomic write (temp file + move) to prevent corruption.
/// Thread-safe via lock.
/// </summary>
public sealed class ConfigService
{
    private static readonly string ConfigDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Viper");

    private static readonly string ConfigFilePath = Path.Combine(ConfigDirectory, "config.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly object _lock = new();

    public ViperConfig Load()
    {
        lock (_lock)
        {
            if (!File.Exists(ConfigFilePath))
                return new ViperConfig();

            try
            {
                string json = File.ReadAllText(ConfigFilePath);
                return JsonSerializer.Deserialize<ViperConfig>(json, JsonOptions) ?? new ViperConfig();
            }
            catch
            {
                return new ViperConfig();
            }
        }
    }

    public void Save(ViperConfig config)
    {
        lock (_lock)
        {
            if (!Directory.Exists(ConfigDirectory))
                Directory.CreateDirectory(ConfigDirectory);

            string tempPath = ConfigFilePath + ".tmp";
            string json = JsonSerializer.Serialize(config, JsonOptions);

            File.WriteAllText(tempPath, json);
            File.Move(tempPath, ConfigFilePath, overwrite: true);
        }
    }
}
