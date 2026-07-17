using System;
using System.IO;
using System.Text.Json;

namespace Viper.Config
{
    public class ConfigStore
    {
        private readonly string _configFilePath;
        private readonly string _configDirectory;
        private readonly object _lock = new object();

        public ConfigStore()
        {
            _configDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Viper");
            _configFilePath = Path.Combine(_configDirectory, "viper.json");
        }

        public ViperConfig Load()
        {
            lock (_lock)
            {
                if (!File.Exists(_configFilePath))
                {
                    return CreateDefaultConfig();
                }

                try
                {
                    string json = File.ReadAllText(_configFilePath);
                    var config = JsonSerializer.Deserialize<ViperConfig>(json);
                    return config ?? CreateDefaultConfig();
                }
                catch (Exception)
                {
                    return CreateDefaultConfig();
                }
            }
        }

        public void Save(ViperConfig config)
        {
            lock (_lock)
            {
                if (!Directory.Exists(_configDirectory))
                {
                    Directory.CreateDirectory(_configDirectory);
                }

                string tempPath = _configFilePath + ".tmp";
                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                
                // Temp file and swap to ensure atomic write.
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, _configFilePath, overwrite: true);
            }
        }

        private ViperConfig CreateDefaultConfig()
        {
            var config = new ViperConfig();
            
            // For Milestone B Pass 1 testing, pre-populate common apps if config is missing
            config.ProtectedApps.Add(new ProtectedApp { Path = "firefox.exe", DisplayName = "Firefox" });
            config.ProtectedApps.Add(new ProtectedApp { Path = "chrome.exe", DisplayName = "Chrome" });
            config.ProtectedApps.Add(new ProtectedApp { Path = "msedge.exe", DisplayName = "Edge" });
            config.ProtectedApps.Add(new ProtectedApp { Path = "GoogleDriveFS.exe", DisplayName = "Google Drive" });
            config.ProtectedApps.Add(new ProtectedApp { Path = "ChatGPT.exe", DisplayName = "ChatGPT" });
            
            return config;
        }
    }
}
