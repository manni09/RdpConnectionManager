using System;
using System.Collections.Generic;

namespace RdpManager.Models
{
    public class RdpConnection
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Hostname { get; set; } = string.Empty;
        public int Port { get; set; } = 3389;
        public string Username { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public string EncryptedPassword { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Group { get; set; } = "Default";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? LastConnected { get; set; }
        
        // RDP Settings
        public bool FullScreen { get; set; } = true;
        public int ScreenWidth { get; set; } = 1920;
        public int ScreenHeight { get; set; } = 1080;
        public bool UseMultiMonitor { get; set; } = false;
        public bool RedirectClipboard { get; set; } = true;
        public bool RedirectPrinters { get; set; } = false;
        public bool RedirectDrives { get; set; } = false;
        public bool AdminSession { get; set; } = false;
        public bool SmartSizing { get; set; } = true;
        public bool DynamicResolution { get; set; } = true;
        
        // Audio Settings
        public int AudioRedirectionMode { get; set; } = 0; // 0=Bring to this computer, 1=Leave at remote, 2=Do not play
        public bool AudioCaptureRedirection { get; set; } = true; // Microphone redirection
        
        // Additional Redirection
        public bool RedirectSmartCards { get; set; } = false;
        public bool RedirectPorts { get; set; } = false;
        public bool RedirectPnPDevices { get; set; } = false;
        
        // Performance Settings
        public bool EnableDesktopComposition { get; set; } = true;
        public bool EnableFontSmoothing { get; set; } = true;
        public bool EnableWindowDrag { get; set; } = true;
        public bool EnableMenuAnimations { get; set; } = false;
        public bool EnableThemes { get; set; } = true;
        public bool EnableBitmapCaching { get; set; } = true;
        
        // Network Settings
        public bool EnableCompression { get; set; } = true;
        public bool NetworkAutoDetect { get; set; } = true;
        
        public string DisplayName => string.IsNullOrEmpty(Name) ? Hostname : Name;
        public string ConnectionString => Port == 3389 ? Hostname : $"{Hostname}:{Port}";
    }

    public class ConnectionGroup
    {
        public string Name { get; set; } = string.Empty;
        public List<RdpConnection> Connections { get; set; } = new();
        public bool IsExpanded { get; set; } = true;
    }

    public class WindowSettings
    {
        public double Left { get; set; } = double.NaN;
        public double Top { get; set; } = double.NaN;
        public double Width { get; set; } = 950;
        public double Height { get; set; } = 650;
        public bool IsMaximized { get; set; } = false;
    }

    public class AppSettings
    {
        public List<RdpConnection> Connections { get; set; } = new();
        public List<string> Groups { get; set; } = new() { "Default" };
        public bool MinimizeToTray { get; set; } = true;
        public bool StartMinimized { get; set; } = false;
        public bool ConfirmOnDelete { get; set; } = true;
        public string DefaultGroup { get; set; } = "Default";
        public WindowSettings MainWindowState { get; set; } = new();
        public WindowSettings RdpSessionWindowState { get; set; } = new() { Width = 1200, Height = 800 };
    }
}
