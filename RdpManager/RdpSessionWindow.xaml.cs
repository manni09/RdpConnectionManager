using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using RdpManager.Controls;
using RdpManager.Models;
using RdpManager.Services;

namespace RdpManager
{
    /// <summary>
    /// Embedded Remote Desktop session window using the Microsoft RDP ActiveX control.
    /// </summary>
    public partial class RdpSessionWindow : Window
    {
        private RdpClientControl? _rdpClient;
        private RdpConnection? _connection;
        private bool _isConnected;
        private bool _isFullScreen;
        private WindowState _previousWindowState;
        private WindowStyle _previousWindowStyle;
        private DispatcherTimer? _statusTimer;
        private DispatcherTimer? _resizeTimer;
        private double _savedWidth;
        private double _savedHeight;
        private double _savedLeft;
        private double _savedTop;
        private bool _stateRestored;

        public RdpSessionWindow()
        {
            // Load saved state BEFORE InitializeComponent so we can apply it
            LoadSavedWindowState();
            
            InitializeComponent();
            
            // Handle window resize with debounce
            SizeChanged += RdpSessionWindow_SizeChanged;
        }

        private void LoadSavedWindowState()
        {
            try
            {
                var settings = StorageService.LoadSettings();
                var ws = settings.RdpSessionWindowState;
                if (ws != null)
                {
                    _savedWidth = ws.Width > 0 ? ws.Width : 1200;
                    _savedHeight = ws.Height > 0 ? ws.Height : 800;
                    _savedLeft = ws.Left;
                    _savedTop = ws.Top;
                    LoggingService.Info($"Loaded saved RDP window state: {_savedWidth}x{_savedHeight}");
                }
                else
                {
                    _savedWidth = 1200;
                    _savedHeight = 800;
                    _savedLeft = double.NaN;
                    _savedTop = double.NaN;
                }
            }
            catch
            {
                _savedWidth = 1200;
                _savedHeight = 800;
                _savedLeft = double.NaN;
                _savedTop = double.NaN;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            RestoreWindowState();
        }

        /// <summary>
        /// Connects to the specified RDP connection.
        /// </summary>
        public void Connect(RdpConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));

            // Register this session
            SessionManagerService.RegisterSession(connection.Id, this);

            // Update UI
            ConnectionNameText.Text = connection.DisplayName;
            ConnectionHostText.Text = connection.ConnectionString;
            Title = $"{connection.DisplayName} - Remote Desktop";

            LoggingService.Info($"Starting embedded RDP session to {connection.ConnectionString}");;

            try
            {
                InitializeRdpClient();
                ConfigureConnection();
                
                StatusText.Text = "Connecting...";
                StatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(255, 193, 7));
                
                _rdpClient!.Connect();
                
                // Start status monitoring timer
                _statusTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1)
                };
                _statusTimer.Tick += StatusTimer_Tick;
                _statusTimer.Start();
            }
            catch (Exception ex)
            {
                LoggingService.Error($"Failed to start RDP session: {ex.Message}", ex);
                MessageBox.Show($"Failed to connect: {ex.Message}", "Connection Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        private void StatusTimer_Tick(object? sender, EventArgs e)
        {
            if (_rdpClient == null) return;

            try
            {
                if (_rdpClient.IsConnected && !_isConnected)
                {
                    _isConnected = true;
                    StatusText.Text = "Connected";
                    StatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                    ResolutionText.Text = $"{_rdpClient.DesktopWidth}x{_rdpClient.DesktopHeight}";
                    LoggingService.Info($"RDP connected to {_connection?.ConnectionString}");
                    
                    if (_connection != null)
                    {
                        _connection.LastConnected = DateTime.Now;
                    }
                    
                    // Update resolution to match current window size for clear display
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        UpdateRemoteResolution();
                    }), DispatcherPriority.Background);
                }
                else if (!_rdpClient.IsConnected && _isConnected)
                {
                    _isConnected = false;
                    StatusText.Text = "Disconnected";
                    StatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(244, 67, 54));
                    LoggingService.Info($"RDP disconnected from {_connection?.ConnectionString}");
                    
                    _statusTimer?.Stop();
                    Close();
                }
            }
            catch
            {
                // Control may be disposed
            }
        }

        /// <summary>
        /// Initializes the RDP ActiveX control.
        /// </summary>
        private void InitializeRdpClient()
        {
            _rdpClient = new RdpClientControl();
            WfHost.Child = _rdpClient;
        }

        /// <summary>
        /// Configures the RDP connection settings.
        /// </summary>
        private void ConfigureConnection()
        {
            if (_rdpClient == null || _connection == null) return;

            // Basic connection settings
            _rdpClient.Server = _connection.Hostname;
            
            // Credentials
            if (!string.IsNullOrEmpty(_connection.Username))
            {
                _rdpClient.UserName = _connection.Username;
            }

            if (!string.IsNullOrEmpty(_connection.Domain))
            {
                _rdpClient.Domain = _connection.Domain;
            }

            // Set password
            if (!string.IsNullOrEmpty(_connection.EncryptedPassword))
            {
                try
                {
                    string password = CredentialService.DecryptPassword(_connection.EncryptedPassword);
                    _rdpClient.SetPassword(password);
                }
                catch (Exception ex)
                {
                    LoggingService.Warn($"Could not set password: {ex.Message}");
                }
            }

            // Display settings - use actual window size for initial connection
            int initialWidth = (int)RdpContainer.ActualWidth;
            int initialHeight = (int)RdpContainer.ActualHeight;
            
            // Fall back to saved/window dimensions if ActualWidth/Height not yet available
            if (initialWidth <= 0) initialWidth = (int)(_savedWidth > 0 ? _savedWidth - 20 : 1180);
            if (initialHeight <= 0) initialHeight = (int)(_savedHeight > 0 ? _savedHeight - 60 : 740);
            
            _rdpClient.DesktopWidth = initialWidth;
            _rdpClient.DesktopHeight = initialHeight;

            _rdpClient.ColorDepth = 32;

            // Advanced settings
            var advSettings = _rdpClient.AdvancedSettings;
            if (advSettings != null)
            {
                try
                {
                    // Connection settings
                    advSettings.RDPPort = _connection.Port;
                    advSettings.EnableCredSspSupport = true;
                    advSettings.AuthenticationLevel = 2;
                    advSettings.EnableAutoReconnect = true;
                    
                    // Device redirection
                    advSettings.RedirectClipboard = _connection.RedirectClipboard;
                    advSettings.RedirectPrinters = _connection.RedirectPrinters;
                    advSettings.RedirectDrives = _connection.RedirectDrives;
                    advSettings.RedirectSmartCards = _connection.RedirectSmartCards;
                    advSettings.RedirectPorts = _connection.RedirectPorts;
                    advSettings.RedirectPOSDevices = _connection.RedirectPnPDevices;
                    
                    // Audio settings
                    advSettings.AudioRedirectionMode = _connection.AudioRedirectionMode;
                    advSettings.AudioCaptureRedirectionMode = _connection.AudioCaptureRedirection ? 1 : 0;
                    
                    // Display settings
                    advSettings.SmartSizing = _connection.SmartSizing;
                    
                    // Performance settings
                    advSettings.PerformanceFlags = GetPerformanceFlags(_connection);
                    advSettings.BitmapPersistence = _connection.EnableBitmapCaching ? 1 : 0;
                    advSettings.Compress = _connection.EnableCompression ? 1 : 0;
                    advSettings.BandwidthDetection = _connection.NetworkAutoDetect;
                    
                    if (_connection.AdminSession)
                    {
                        advSettings.ConnectToAdministerServer = true;
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.Warn($"Could not set all advanced settings: {ex.Message}");
                }
            }

            LoggingService.Debug($"RDP configured: {_rdpClient.DesktopWidth}x{_rdpClient.DesktopHeight}");
        }

        /// <summary>
        /// Calculates performance flags based on connection settings.
        /// </summary>
        private int GetPerformanceFlags(RdpConnection connection)
        {
            // Performance flags are a bitmask
            // TS_PERF_DISABLE_WALLPAPER           = 0x00000001
            // TS_PERF_DISABLE_FULLWINDOWDRAG      = 0x00000002
            // TS_PERF_DISABLE_MENUANIMATIONS      = 0x00000004
            // TS_PERF_DISABLE_THEMING             = 0x00000008
            // TS_PERF_DISABLE_CURSOR_SHADOW       = 0x00000020
            // TS_PERF_DISABLE_CURSORSETTINGS      = 0x00000040
            // TS_PERF_ENABLE_FONT_SMOOTHING       = 0x00000080
            // TS_PERF_ENABLE_DESKTOP_COMPOSITION  = 0x00000100
            
            int flags = 0;
            
            // We want to ENABLE these (so don't set the disable flag)
            // But the flags are "disable" flags, so we set them when we DON'T want the feature
            
            if (!connection.EnableWindowDrag)
                flags |= 0x00000002;
            
            if (!connection.EnableMenuAnimations)
                flags |= 0x00000004;
            
            if (!connection.EnableThemes)
                flags |= 0x00000008;
            
            // These are enable flags (opposite logic)
            if (connection.EnableFontSmoothing)
                flags |= 0x00000080;
            
            if (connection.EnableDesktopComposition)
                flags |= 0x00000100;
            
            return flags;
        }

        #region UI Events

        private void FullScreen_Click(object sender, RoutedEventArgs e)
        {
            ToggleFullScreen();
        }

        private void Disconnect_Click(object sender, RoutedEventArgs e)
        {
            DisconnectSession();
            Close();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveWindowState();
            
            // Unregister this session
            if (_connection != null)
            {
                SessionManagerService.UnregisterSession(_connection.Id);
            }
            
            DisconnectSession();
        }

        private void RestoreWindowState()
        {
            if (_stateRestored) return;
            _stateRestored = true;
            
            try
            {
                // Apply the pre-loaded saved state
                Width = _savedWidth;
                Height = _savedHeight;

                // Restore position, or center if not saved
                if (!double.IsNaN(_savedLeft) && !double.IsNaN(_savedTop))
                {
                    Left = _savedLeft;
                    Top = _savedTop;
                }
                else
                {
                    Left = (SystemParameters.PrimaryScreenWidth - Width) / 2;
                    Top = (SystemParameters.PrimaryScreenHeight - Height) / 2;
                }

                LoggingService.Info($"Applied RDP window state: {Width}x{Height} at ({Left},{Top})");
            }
            catch (Exception ex)
            {
                LoggingService.Warn($"Could not restore RDP window state: {ex.Message}");
                Left = (SystemParameters.PrimaryScreenWidth - Width) / 2;
                Top = (SystemParameters.PrimaryScreenHeight - Height) / 2;
            }
        }

        private void SaveWindowState()
        {
            try
            {
                var settings = StorageService.LoadSettings();
                settings.RdpSessionWindowState ??= new WindowSettings();
                var ws = settings.RdpSessionWindowState;

                ws.IsMaximized = WindowState == WindowState.Maximized;

                // Use RestoreBounds to get the normal window size even when maximized
                if (WindowState == WindowState.Normal)
                {
                    ws.Left = Left;
                    ws.Top = Top;
                    ws.Width = ActualWidth > 0 ? ActualWidth : Width;
                    ws.Height = ActualHeight > 0 ? ActualHeight : Height;
                }
                else if (RestoreBounds != Rect.Empty)
                {
                    ws.Left = RestoreBounds.Left;
                    ws.Top = RestoreBounds.Top;
                    ws.Width = RestoreBounds.Width;
                    ws.Height = RestoreBounds.Height;
                }

                StorageService.SaveSettings(settings);
                LoggingService.Info($"Saved RDP window state: {ws.Width}x{ws.Height} at ({ws.Left},{ws.Top}), Maximized={ws.IsMaximized}");
            }
            catch (Exception ex)
            {
                LoggingService.Warn($"Could not save RDP window state: {ex.Message}");
            }
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                ToggleToolbar();
                e.Handled = true;
            }
        }

        private void TopTrigger_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            ShowToolbar();
        }

        private void Toolbar_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            HideToolbar();
        }

        private void ShowToolbar()
        {
            ToolbarBorder.Visibility = Visibility.Visible;
            StatusBarBorder.Visibility = Visibility.Visible;
        }

        private void HideToolbar()
        {
            ToolbarBorder.Visibility = Visibility.Collapsed;
            StatusBarBorder.Visibility = Visibility.Collapsed;
        }

        private void ToggleToolbar()
        {
            if (ToolbarBorder.Visibility == Visibility.Visible)
            {
                HideToolbar();
            }
            else
            {
                ShowToolbar();
            }
        }

        private void ToggleFullScreen()
        {
            if (_isFullScreen)
            {
                WindowStyle = _previousWindowStyle;
                WindowState = _previousWindowState;
                _isFullScreen = false;
                FullScreenBtn.Content = "⛶ Full Screen";
            }
            else
            {
                _previousWindowState = WindowState;
                _previousWindowStyle = WindowStyle;
                WindowStyle = WindowStyle.None;
                WindowState = WindowState.Maximized;
                _isFullScreen = true;
                FullScreenBtn.Content = "⛶ Exit Full Screen";
            }
        }

        private void DisconnectSession()
        {
            _statusTimer?.Stop();
            _resizeTimer?.Stop();
            
            if (_rdpClient != null)
            {
                try
                {
                    if (_rdpClient.IsConnected)
                    {
                        _rdpClient.Disconnect();
                        LoggingService.Info($"User disconnected from {_connection?.ConnectionString}");
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.Warn($"Error during disconnect: {ex.Message}");
                }

                try
                {
                    _rdpClient.Dispose();
                }
                catch { }
                
                _rdpClient = null;
            }
        }

        /// <summary>
        /// Handles window resize with debounce to avoid excessive updates.
        /// </summary>
        private void RdpSessionWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!_isConnected || _rdpClient == null) return;

            // Debounce resize events - wait 300ms after last resize before updating
            _resizeTimer?.Stop();
            _resizeTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            _resizeTimer.Tick += (s, args) =>
            {
                _resizeTimer?.Stop();
                UpdateRemoteResolution();
            };
            _resizeTimer.Start();
        }

        /// <summary>
        /// Updates the remote session to match the current window size.
        /// </summary>
        private void UpdateRemoteResolution()
        {
            if (_rdpClient == null || !_isConnected) return;

            try
            {
                int newWidth = (int)RdpContainer.ActualWidth;
                int newHeight = (int)RdpContainer.ActualHeight;

                if (newWidth > 0 && newHeight > 0)
                {
                    _rdpClient.UpdateSessionDisplaySettings(newWidth, newHeight);
                    ResolutionText.Text = $"{newWidth}x{newHeight}";
                    LoggingService.Debug($"Updated RDP resolution to {newWidth}x{newHeight}");
                }
            }
            catch (Exception ex)
            {
                LoggingService.Warn($"Could not update resolution: {ex.Message}");
            }
        }

        #endregion
    }
}
