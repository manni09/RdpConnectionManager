using System;
using System.IO;
using Newtonsoft.Json;
using RdpManager.Models;

namespace RdpManager.Services
{
    /// <summary>
    /// Service for persisting application settings and connections to disk.
    /// </summary>
    public static class StorageService
    {
        private static readonly string AppDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RdpManager");
        
        private static readonly string SettingsFilePath = Path.Combine(AppDataFolder, "settings.json");
        private static readonly string BackupFilePath = Path.Combine(AppDataFolder, "settings.backup.json");

        /// <summary>
        /// Loads application settings from disk.
        /// </summary>
        public static AppSettings LoadSettings()
        {
            try
            {
                LoggingService.Debug($"Loading settings from {SettingsFilePath}");
                
                if (!Directory.Exists(AppDataFolder))
                {
                    LoggingService.Info($"Creating app data folder: {AppDataFolder}");
                    Directory.CreateDirectory(AppDataFolder);
                }

                if (File.Exists(SettingsFilePath))
                {
                    string json = File.ReadAllText(SettingsFilePath);
                    var settings = JsonConvert.DeserializeObject<AppSettings>(json);
                    LoggingService.Debug($"Settings loaded successfully with {settings?.Connections.Count ?? 0} connections");
                    return settings ?? new AppSettings();
                }
                
                LoggingService.Info("No settings file found, returning defaults");
            }
            catch (Exception ex)
            {
                LoggingService.Warn($"Failed to load settings: {ex.Message}");
                
                // Try to load from backup
                try
                {
                    if (File.Exists(BackupFilePath))
                    {
                        LoggingService.Info("Attempting to restore from backup...");
                        string json = File.ReadAllText(BackupFilePath);
                        var settings = JsonConvert.DeserializeObject<AppSettings>(json);
                        if (settings != null)
                        {
                            LoggingService.Info("Successfully restored settings from backup");
                            SaveSettings(settings); // Restore from backup
                            return settings;
                        }
                    }
                }
                catch (Exception backupEx)
                {
                    LoggingService.Error("Backup restoration also failed", backupEx);
                }
            }

            return new AppSettings();
        }

        /// <summary>
        /// Saves application settings to disk.
        /// </summary>
        public static void SaveSettings(AppSettings settings)
        {
            try
            {
                LoggingService.Debug($"Saving settings with {settings.Connections.Count} connections");
                
                if (!Directory.Exists(AppDataFolder))
                {
                    Directory.CreateDirectory(AppDataFolder);
                }

                // Create backup of existing file
                if (File.Exists(SettingsFilePath))
                {
                    File.Copy(SettingsFilePath, BackupFilePath, overwrite: true);
                    LoggingService.Debug("Created backup of previous settings");
                }

                string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(SettingsFilePath, json);
                LoggingService.Debug("Settings saved successfully");
            }
            catch (Exception ex)
            {
                LoggingService.Error("Failed to save settings", ex);
                throw new InvalidOperationException($"Failed to save settings: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Exports connections to a file (without passwords for security).
        /// </summary>
        public static void ExportConnections(AppSettings settings, string filePath, bool includePasswords = false)
        {
            var exportSettings = new AppSettings
            {
                Connections = settings.Connections.ConvertAll(c => new RdpConnection
                {
                    Id = c.Id,
                    Name = c.Name,
                    Hostname = c.Hostname,
                    Port = c.Port,
                    Username = c.Username,
                    Domain = c.Domain,
                    EncryptedPassword = includePasswords ? c.EncryptedPassword : string.Empty,
                    Description = c.Description,
                    Group = c.Group,
                    FullScreen = c.FullScreen,
                    ScreenWidth = c.ScreenWidth,
                    ScreenHeight = c.ScreenHeight,
                    UseMultiMonitor = c.UseMultiMonitor,
                    RedirectClipboard = c.RedirectClipboard,
                    RedirectPrinters = c.RedirectPrinters,
                    RedirectDrives = c.RedirectDrives,
                    AdminSession = c.AdminSession
                }),
                Groups = settings.Groups
            };

            string json = JsonConvert.SerializeObject(exportSettings, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }

        /// <summary>
        /// Imports connections from a file.
        /// </summary>
        public static AppSettings ImportConnections(string filePath)
        {
            string json = File.ReadAllText(filePath);
            var importedSettings = JsonConvert.DeserializeObject<AppSettings>(json);
            return importedSettings ?? new AppSettings();
        }

        /// <summary>
        /// Gets the settings file path for display purposes.
        /// </summary>
        public static string GetSettingsPath() => SettingsFilePath;
    }
}
