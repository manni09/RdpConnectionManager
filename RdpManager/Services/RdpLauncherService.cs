using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using RdpManager.Models;

namespace RdpManager.Services
{
    /// <summary>
    /// Service to launch RDP and SSH connections
    /// </summary>
    public static class RdpLauncherService
    {
        /// <summary>
        /// Gets or sets whether to use the embedded RDP client instead of mstsc.exe
        /// </summary>
        public static bool UseEmbeddedClient { get; set; } = true;

        /// <summary>
        /// Launches a connection (RDP or SSH based on connection type).
        /// </summary>
        public static void Connect(RdpConnection connection)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            // Route to appropriate handler based on connection type
            if (connection.ConnectionType == ConnectionType.SSH)
            {
                ConnectSsh(connection);
            }
            else if (UseEmbeddedClient)
            {
                ConnectEmbedded(connection);
            }
            else
            {
                ConnectExternal(connection);
            }
        }

        /// <summary>
        /// Connects to an SSH server using the embedded terminal.
        /// </summary>
        public static void ConnectSsh(RdpConnection connection)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            // Check if there's already an active session for this connection
            if (SessionManagerService.HasActiveSession(connection.Id))
            {
                LoggingService.Info($"Found existing SSH session for {connection.ConnectionString}, focusing it");
                SessionManagerService.FocusSession(connection.Id);
                return;
            }

            LoggingService.Info($"Initiating SSH connection to {connection.ConnectionString}");

            try
            {
                var sessionWindow = new SshSessionWindow();
                sessionWindow.Show();
                sessionWindow.Connect(connection);
                
                // Update last connected time
                connection.LastConnected = DateTime.Now;
            }
            catch (Exception ex)
            {
                LoggingService.Error($"Failed to start SSH session: {ex.Message}", ex);
                MessageBox.Show($"Failed to connect: {ex.Message}", 
                    "SSH Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Connects using the embedded RDP client (ActiveX control).
        /// </summary>
        public static void ConnectEmbedded(RdpConnection connection)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            // Check if there's already an active session for this connection
            if (SessionManagerService.HasActiveSession(connection.Id))
            {
                LoggingService.Info($"Found existing session for {connection.ConnectionString}, focusing it");
                SessionManagerService.FocusSession(connection.Id);
                return;
            }

            LoggingService.Info($"Initiating embedded RDP connection to {connection.ConnectionString}");

            try
            {
                var sessionWindow = new RdpSessionWindow();
                sessionWindow.Show();
                sessionWindow.Connect(connection);
                
                // Update last connected time
                connection.LastConnected = DateTime.Now;
            }
            catch (Exception ex)
            {
                LoggingService.Error($"Failed to start embedded RDP session: {ex.Message}", ex);
                MessageBox.Show($"Failed to connect: {ex.Message}\n\nFalling back to mstsc.exe...", 
                    "Connection Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                ConnectExternal(connection);
            }
        }

        /// <summary>
        /// Connects using the external Windows Remote Desktop client (mstsc.exe).
        /// </summary>
        public static void ConnectExternal(RdpConnection connection)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            LoggingService.Info($"Initiating external RDP connection to {connection.ConnectionString}");

            // Store credentials in Windows Credential Manager for seamless login
            if (!string.IsNullOrEmpty(connection.Username) && !string.IsNullOrEmpty(connection.EncryptedPassword))
            {
                LoggingService.Debug("Storing credentials in Windows Credential Manager");
                string password = CredentialService.DecryptPassword(connection.EncryptedPassword);
                string fullUsername = string.IsNullOrEmpty(connection.Domain) 
                    ? connection.Username 
                    : $"{connection.Domain}\\{connection.Username}";
                
                // Use just the hostname for credential storage (without port)
                CredentialService.StoreWindowsCredential(connection.Hostname, fullUsername, password);
            }
            else
            {
                LoggingService.Debug("No credentials configured, will prompt for login");
            }

            // Generate temporary .rdp file with settings
            string rdpFilePath = GenerateRdpFile(connection);
            LoggingService.Debug($"Generated RDP file: {rdpFilePath}");

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "mstsc.exe",
                    Arguments = $"\"{rdpFilePath}\"",
                    UseShellExecute = true
                };

                Process.Start(startInfo);
                LoggingService.Info($"RDP client launched for {connection.ConnectionString}");
            }
            catch (Exception ex)
            {
                LoggingService.Error($"Failed to launch mstsc.exe for {connection.ConnectionString}", ex);
                throw new InvalidOperationException($"Failed to launch Remote Desktop: {ex.Message}", ex);
            }
            finally
            {
                // Clean up the temp file after a short delay
                CleanupRdpFileAsync(rdpFilePath);
            }
        }

        /// <summary>
        /// Generates a temporary .rdp file with the connection settings.
        /// </summary>
        private static string GenerateRdpFile(RdpConnection connection)
        {
            var sb = new StringBuilder();
            
            // Basic connection settings
            sb.AppendLine($"full address:s:{connection.ConnectionString}");
            sb.AppendLine($"prompt for credentials:i:0");
            
            // Screen settings
            if (connection.FullScreen)
            {
                sb.AppendLine("screen mode id:i:2");
            }
            else
            {
                sb.AppendLine("screen mode id:i:1");
                sb.AppendLine($"desktopwidth:i:{connection.ScreenWidth}");
                sb.AppendLine($"desktopheight:i:{connection.ScreenHeight}");
            }

            // Multi-monitor
            if (connection.UseMultiMonitor)
            {
                sb.AppendLine("use multimon:i:1");
            }

            // Username (password is handled via Credential Manager)
            if (!string.IsNullOrEmpty(connection.Username))
            {
                string fullUsername = string.IsNullOrEmpty(connection.Domain)
                    ? connection.Username
                    : $"{connection.Domain}\\{connection.Username}";
                sb.AppendLine($"username:s:{fullUsername}");
            }

            // Redirection settings
            sb.AppendLine($"redirectclipboard:i:{(connection.RedirectClipboard ? 1 : 0)}");
            sb.AppendLine($"redirectprinters:i:{(connection.RedirectPrinters ? 1 : 0)}");
            sb.AppendLine($"redirectdrives:i:{(connection.RedirectDrives ? 1 : 0)}");
            
            // Admin session
            if (connection.AdminSession)
            {
                sb.AppendLine("administrative session:i:1");
            }

            // Auto resize and refresh settings
            sb.AppendLine($"smart sizing:i:{(connection.SmartSizing ? 1 : 0)}");
            sb.AppendLine($"dynamic resolution:i:{(connection.DynamicResolution ? 1 : 0)}");

            // Additional recommended settings
            sb.AppendLine("authentication level:i:2");
            sb.AppendLine("enablecredsspsupport:i:1");
            sb.AppendLine("disable wallpaper:i:0");
            sb.AppendLine("allow font smoothing:i:1");
            sb.AppendLine("allow desktop composition:i:1");
            sb.AppendLine("bitmapcachepersistenable:i:1");
            sb.AppendLine("connection type:i:7");
            sb.AppendLine("networkautodetect:i:1");
            sb.AppendLine("bandwidthautodetect:i:1");
            sb.AppendLine("autoreconnection enabled:i:1");

            // Write to temp file
            string tempPath = Path.Combine(Path.GetTempPath(), $"rdp_{connection.Id}.rdp");
            File.WriteAllText(tempPath, sb.ToString());
            
            return tempPath;
        }

        /// <summary>
        /// Cleans up the temporary RDP file after mstsc has read it.
        /// </summary>
        private static async void CleanupRdpFileAsync(string filePath)
        {
            try
            {
                // Wait a bit for mstsc to read the file
                await System.Threading.Tasks.Task.Delay(5000);
                
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch
            {
                // Ignore cleanup failures
            }
        }

        /// <summary>
        /// Launches a quick connect to a hostname without saving.
        /// </summary>
        public static void QuickConnect(string hostname, int port = 3389)
        {
            var connection = new RdpConnection
            {
                Hostname = hostname,
                Port = port,
                FullScreen = true
            };
            Connect(connection);
        }
    }
}
