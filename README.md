# RDP Connection Manager

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows-0078D6)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

**A powerful, modern RDP connection manager for Windows power users and IT professionals.**

Missing the discontinued Microsoft Remote Desktop Connection Manager (RDCMan)? RDP Connection Manager brings back everything you loved—and adds much more. Manage all your remote desktop connections in one sleek, organized interface with **embedded RDP sessions** that run directly inside the application window.

Connect to **Windows**, **Linux** (via [xrdp](http://xrdp.org/)), or any system running an RDP-compatible server. No more juggling dozens of mstsc.exe windows. No more re-entering credentials. Just fast, secure, and organized remote desktop management.

---

## Key Features

- **Embedded RDP Client** - Full RDP sessions inside the application window (no separate mstsc.exe windows)
- **Cross-platform targets** - Connect to Windows, Linux (xrdp), or any RDP-compatible server
- **Store connections** - Save server hostname, port, username, domain, and password
- **Secure credential storage** - Passwords encrypted using Windows DPAPI (tied to your user account)
- **Quick Connect** - Connect to any server instantly without saving
- **Groups** - Organize connections into groups
- **Session Tracking** - Visual indicators for active sessions, one-click to bring session to front
- **Auto-resize Display** - RDP resolution adjusts dynamically when you resize the window
- **Audio & Device Redirection** - Audio playback/capture, smart cards, ports, PnP devices
- **Performance Settings** - Desktop composition, font smoothing, themes, animations
- **Connection settings** - Full screen, multi-monitor, clipboard/printer/drive redirection
- **Import/Export** - Backup and share connections (passwords optional)
- **Modern UI** - Clean, professional interface with window state persistence

## Why Choose RDP Connection Manager?

| Challenge | Solution |
|-----------|----------|
| Managing dozens of servers | Organized groups with quick search |
| Remembering credentials | Secure DPAPI-encrypted storage |
| Window clutter from mstsc.exe | Embedded sessions in tabbed windows |
| Connecting after window resize | Auto-adjusting resolution for crisp display |
| Finding your active sessions | Visual indicators + one-click focus |
| Backing up connections | JSON import/export with optional passwords |

## Requirements

- Windows 10/11
- .NET 10.0 Runtime (or self-contained release)

## Installation

### Option 1: Download Release

1. Download `RdpManager-v1.0.0-win-x64.zip` from Releases
2. Extract and run `RdpManager.exe` (self-contained, no .NET install required)

### Option 2: Build from Source

1. Install [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
2. Open terminal in project folder:
   ```powershell
   cd c:\Source\RdpManager
   dotnet build -c Release
   ```
3. Run from: `bin\Release\net10.0-windows\RdpManager.exe`

### Option 3: Open in Visual Studio

1. Open `RdpManager.sln` in Visual Studio 2022
2. Build and run (F5)

## Usage

### Adding Connections

1. Click **"+ New Connection"** or press `Ctrl+N`
2. Enter the hostname/IP address (required)
3. Optionally fill in: display name, username, password, domain
4. Configure display and redirection settings
5. Click **Save**

### Connecting

- **Double-click** a connection in the list, or
- Select a connection and click **Connect**, or
- Press `Enter` with a connection selected

### Quick Connect

Type a hostname in the Quick Connect box and press Enter to connect without saving.

### Import/Export

- **Export**: Saves all connections to a JSON file. You can choose whether to include passwords.
- **Import**: Load connections from a previously exported JSON file.

## Data Storage

Settings are stored in:
```
%LOCALAPPDATA%\RdpManager\settings.json
```

Passwords are encrypted using Windows Data Protection API (DPAPI) and can only be decrypted by the same Windows user account that created them.

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `Ctrl+N` | New connection |
| `Enter` | Connect to selected |
| `Delete` | Delete selected connection |

## Security Notes

- Passwords are encrypted with DPAPI (CurrentUser scope)
- Encrypted passwords are tied to your Windows user account
- Exported passwords won't work on other computers or user accounts
- Credentials are stored in Windows Credential Manager for seamless RDP login

For more details, see [SECURITY.md](SECURITY.md).

## Built With

- [.NET 10.0](https://dotnet.microsoft.com/) - Framework
- [WPF](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/) - Windows Presentation Foundation
- [mstscax.dll](https://docs.microsoft.com/en-us/windows/win32/termserv/remote-desktop-activex-control) - Microsoft RDP ActiveX Control
- [Newtonsoft.Json](https://www.newtonsoft.com/json) - JSON serialization

## Contributing

Contributions are welcome! Feel free to:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

Copyright (c) 2026

---

**Made with care for IT professionals who manage remote servers daily.**
