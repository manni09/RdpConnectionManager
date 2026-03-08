using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using RdpManager.Models;
using RdpManager.Services;

namespace RdpManager
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private AppSettings _settings;
        private ObservableCollection<RdpConnection> _connections;
        private ObservableCollection<RdpConnection> _filteredConnections;
        private RdpConnection? _selectedConnection;
        private string _searchText = string.Empty;
        private string _quickConnectHost = string.Empty;
        private string _statusText = "Ready";

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<RdpConnection> FilteredConnections
        {
            get => _filteredConnections;
            set { _filteredConnections = value; OnPropertyChanged(); }
        }

        public RdpConnection? SelectedConnection
        {
            get => _selectedConnection;
            set
            {
                _selectedConnection = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelection));
                if (value != null)
                {
                    LoggingService.Debug($"Selected connection: {value.DisplayName}");
                }
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                FilterConnections();
            }
        }

        public string QuickConnectHost
        {
            get => _quickConnectHost;
            set { _quickConnectHost = value; OnPropertyChanged(); }
        }

        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        public bool HasSelection => SelectedConnection != null;
        public bool IsListEmpty => _connections.Count == 0;
        public int ConnectionCount => _connections.Count;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            _connections = new ObservableCollection<RdpConnection>();
            _filteredConnections = new ObservableCollection<RdpConnection>();
            _settings = new AppSettings();

            LoggingService.Info("MainWindow initialized");
            LoadSettings();
            RestoreWindowState();
            Closing += MainWindow_Closing;
            
            // Subscribe to session changes to update the UI
            SessionManagerService.SessionsChanged += OnSessionsChanged;
        }

        private void OnSessionsChanged(object? sender, EventArgs e)
        {
            // Refresh the list on UI thread to update active session indicators
            Dispatcher.BeginInvoke(new Action(() =>
            {
                // Force refresh of the list by triggering property change
                var selected = SelectedConnection;
                FilterConnections();
                SelectedConnection = selected;
                
                int activeCount = SessionManagerService.ActiveSessionCount;
                if (activeCount > 0)
                {
                    StatusText = $"{activeCount} active session{(activeCount != 1 ? "s" : "")}";
                }
            }));
        }

        private void LoadSettings()
        {
            try
            {
                LoggingService.Info("Loading settings...");
                _settings = StorageService.LoadSettings();
                _connections = new ObservableCollection<RdpConnection>(_settings.Connections);
                
                // Deduplicate groups and ensure "Default" is first
                CleanupGroups();
                
                FilterConnections();
                StatusText = $"Loaded {_connections.Count} connections";
                LoggingService.Info($"Loaded {_connections.Count} connections");
                OnPropertyChanged(nameof(ConnectionCount));
                OnPropertyChanged(nameof(IsListEmpty));
            }
            catch (Exception ex)
            {
                LoggingService.Error("Failed to load settings", ex);
                MessageBox.Show($"Failed to load settings: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CleanupGroups()
        {
            // Get unique groups, case-insensitive
            var uniqueGroups = _settings.Groups
                .Where(g => !string.IsNullOrWhiteSpace(g))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Add groups from connections that might not be in the list
            foreach (var conn in _connections)
            {
                if (!string.IsNullOrWhiteSpace(conn.Group) &&
                    !uniqueGroups.Any(g => g.Equals(conn.Group, StringComparison.OrdinalIgnoreCase)))
                {
                    uniqueGroups.Add(conn.Group);
                }
            }

            // Ensure "Default" is always first
            uniqueGroups.Remove("Default");
            uniqueGroups.Insert(0, "Default");

            _settings.Groups = uniqueGroups;
        }

        private void SaveSettings()
        {
            try
            {
                // Reload settings to preserve changes made by other windows (RdpSessionWindow)
                var currentSettings = StorageService.LoadSettings();
                
                // Deduplicate groups before saving
                CleanupGroups();
                
                // Update only what MainWindow manages
                currentSettings.Connections = _connections.ToList();
                currentSettings.Groups = _settings.Groups;
                currentSettings.MinimizeToTray = _settings.MinimizeToTray;
                currentSettings.StartMinimized = _settings.StartMinimized;
                currentSettings.ConfirmOnDelete = _settings.ConfirmOnDelete;
                currentSettings.DefaultGroup = _settings.DefaultGroup;
                
                // Save MainWindow state
                currentSettings.MainWindowState ??= new WindowSettings();
                var ws = currentSettings.MainWindowState;
                ws.IsMaximized = WindowState == WindowState.Maximized;
                if (WindowState == WindowState.Normal)
                {
                    ws.Left = Left;
                    ws.Top = Top;
                    ws.Width = Width;
                    ws.Height = Height;
                }
                
                StorageService.SaveSettings(currentSettings);
                _settings = currentSettings;
                LoggingService.Debug("Settings saved successfully");
                StatusText = "Settings saved";
            }
            catch (Exception ex)
            {
                LoggingService.Error("Failed to save settings", ex);
                MessageBox.Show($"Failed to save settings: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RestoreWindowState()
        {
            var ws = _settings.MainWindowState;
            if (ws == null) return;

            if (!double.IsNaN(ws.Left) && !double.IsNaN(ws.Top))
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = ws.Left;
                Top = ws.Top;
            }

            if (ws.Width > 0) Width = ws.Width;
            if (ws.Height > 0) Height = ws.Height;

            if (ws.IsMaximized)
            {
                WindowState = WindowState.Maximized;
            }

            LoggingService.Debug($"Restored window state: {ws.Width}x{ws.Height} at ({ws.Left},{ws.Top}), Maximized={ws.IsMaximized}");
        }

        private void SaveWindowState()
        {
            _settings.MainWindowState ??= new WindowSettings();
            var ws = _settings.MainWindowState;

            ws.IsMaximized = WindowState == WindowState.Maximized;

            if (WindowState == WindowState.Normal)
            {
                ws.Left = Left;
                ws.Top = Top;
                ws.Width = Width;
                ws.Height = Height;
            }

            LoggingService.Debug($"Saved window state: {ws.Width}x{ws.Height} at ({ws.Left},{ws.Top}), Maximized={ws.IsMaximized}");
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            // Unsubscribe from session changes
            SessionManagerService.SessionsChanged -= OnSessionsChanged;
            SaveSettings();
        }

        private void FilterConnections()
        {
            var filtered = string.IsNullOrWhiteSpace(SearchText)
                ? _connections.ToList()
                : _connections.Where(c =>
                    c.DisplayName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    c.Hostname.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    c.Group.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    c.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            FilteredConnections = new ObservableCollection<RdpConnection>(
                filtered.OrderBy(c => c.Group).ThenBy(c => c.DisplayName));
        }

        private void QuickConnect_Click(object sender, RoutedEventArgs e)
        {
            PerformQuickConnect();
        }

        private void QuickConnectBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                PerformQuickConnect();
            }
        }

        private void PerformQuickConnect()
        {
            if (string.IsNullOrWhiteSpace(QuickConnectHost))
            {
                MessageBox.Show("Please enter a hostname or IP address.", "Quick Connect",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                string host = QuickConnectHost.Trim();
                int port = 3389;

                // Parse host:port format
                if (host.Contains(':'))
                {
                    var parts = host.Split(':');
                    host = parts[0];
                    if (parts.Length > 1 && int.TryParse(parts[1], out int parsedPort))
                    {
                        port = parsedPort;
                    }
                }

                LoggingService.Info($"Quick connecting to {host}:{port}");
                RdpLauncherService.QuickConnect(host, port);
                StatusText = $"Connecting to {host}...";
            }
            catch (Exception ex)
            {
                LoggingService.Error($"Quick connect failed to {QuickConnectHost}", ex);
                MessageBox.Show($"Failed to connect: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddConnection_Click(object sender, RoutedEventArgs e)
        {
            LoggingService.Debug("Opening add connection dialog");
            var dialog = new ConnectionEditorWindow(_settings.Groups);
            dialog.Owner = this;

            if (dialog.ShowDialog() == true && dialog.Connection != null)
            {
                _connections.Add(dialog.Connection);
                
                // Add new group if it doesn't exist
                if (!_settings.Groups.Any(g => g.Equals(dialog.Connection.Group, StringComparison.OrdinalIgnoreCase)))
                {
                    _settings.Groups.Add(dialog.Connection.Group);
                }
                
                SaveSettings();
                FilterConnections();
                SelectedConnection = dialog.Connection;
                OnPropertyChanged(nameof(ConnectionCount));
                OnPropertyChanged(nameof(IsListEmpty));
                StatusText = $"Added connection: {dialog.Connection.DisplayName}";
                LoggingService.Info($"Added new connection: {dialog.Connection.DisplayName} ({dialog.Connection.Hostname})");
            }
        }

        private void EditConnection_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedConnection == null) return;

            var dialog = new ConnectionEditorWindow(_settings.Groups, SelectedConnection);
            dialog.Owner = this;

            if (dialog.ShowDialog() == true && dialog.Connection != null)
            {
                // Update the connection in the list
                var index = _connections.IndexOf(SelectedConnection);
                if (index >= 0)
                {
                    _connections[index] = dialog.Connection;
                }
                
                // Add new group if it doesn't exist
                if (!_settings.Groups.Any(g => g.Equals(dialog.Connection.Group, StringComparison.OrdinalIgnoreCase)))
                {
                    _settings.Groups.Add(dialog.Connection.Group);
                }
                
                SaveSettings();
                FilterConnections();
                SelectedConnection = dialog.Connection;
                StatusText = $"Updated connection: {dialog.Connection.DisplayName}";
            }
        }

        private void DeleteConnection_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedConnection == null) return;

            if (_settings.ConfirmOnDelete)
            {
                var result = MessageBox.Show(
                    $"Are you sure you want to delete '{SelectedConnection.DisplayName}'?",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes) return;
            }

            var name = SelectedConnection.DisplayName;
            _connections.Remove(SelectedConnection);
            SelectedConnection = null;
            SaveSettings();
            FilterConnections();
            OnPropertyChanged(nameof(ConnectionCount));
            OnPropertyChanged(nameof(IsListEmpty));
            StatusText = $"Deleted connection: {name}";
        }

        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            ConnectToSelected();
        }

        private void ConnectionList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ConnectToSelected();
        }

        private void ConnectToSelected()
        {
            if (SelectedConnection == null) return;

            try
            {
                // Check if there's already an active session
                if (SessionManagerService.HasActiveSession(SelectedConnection.Id))
                {
                    LoggingService.Info($"Focusing existing session for {SelectedConnection.DisplayName}");
                    SessionManagerService.FocusSession(SelectedConnection.Id);
                    StatusText = $"Switched to {SelectedConnection.DisplayName}";
                    return;
                }

                LoggingService.Info($"Connecting to {SelectedConnection.DisplayName} ({SelectedConnection.ConnectionString})");
                SelectedConnection.LastConnected = DateTime.Now;
                SaveSettings();

                RdpLauncherService.Connect(SelectedConnection);
                StatusText = $"Connecting to {SelectedConnection.DisplayName}...";
            }
            catch (Exception ex)
            {
                LoggingService.Error($"Failed to connect to {SelectedConnection.DisplayName}", ex);
                MessageBox.Show($"Failed to connect: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            FilterConnections();
        }

        private void Import_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                Title = "Import Connections"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var imported = StorageService.ImportConnections(dialog.FileName);
                    int count = 0;

                    foreach (var conn in imported.Connections)
                    {
                        // Generate new ID to avoid conflicts
                        conn.Id = Guid.NewGuid().ToString();
                        _connections.Add(conn);
                        count++;
                    }

                    // Add any new groups
                    foreach (var group in imported.Groups)
                    {
                        if (!_settings.Groups.Contains(group))
                        {
                            _settings.Groups.Add(group);
                        }
                    }

                    SaveSettings();
                    FilterConnections();
                    OnPropertyChanged(nameof(ConnectionCount));
                    OnPropertyChanged(nameof(IsListEmpty));
                    StatusText = $"Imported {count} connections";

                    MessageBox.Show($"Successfully imported {count} connections.", "Import Complete",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to import: {ex.Message}", "Import Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json",
                Title = "Export Connections",
                FileName = "rdp-connections.json"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var result = MessageBox.Show(
                        "Include passwords in export?\n\n" +
                        "Note: Passwords are encrypted with your Windows account and may not work on other computers.",
                        "Export Options",
                        MessageBoxButton.YesNoCancel,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Cancel) return;

                    StorageService.ExportConnections(_settings, dialog.FileName, result == MessageBoxResult.Yes);
                    StatusText = $"Exported to {dialog.FileName}";

                    MessageBox.Show($"Successfully exported {_connections.Count} connections.", "Export Complete",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to export: {ex.Message}", "Export Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            SaveSettings();
            base.OnClosing(e);
        }
    }
}
