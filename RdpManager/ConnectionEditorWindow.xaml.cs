using System;
using System.Collections.Generic;
using System.Windows;
using RdpManager.Models;
using RdpManager.Services;

namespace RdpManager
{
    public partial class ConnectionEditorWindow : Window
    {
        public RdpConnection? Connection { get; private set; }
        private readonly RdpConnection? _existingConnection;
        private readonly bool _isEditing;

        public ConnectionEditorWindow(List<string> groups, RdpConnection? existingConnection = null)
        {
            InitializeComponent();

            _existingConnection = existingConnection;
            _isEditing = existingConnection != null;

            // Setup group combo
            GroupCombo.ItemsSource = groups;
            GroupCombo.SelectedIndex = 0;

            // Setup full screen toggle
            FullScreenCheck.Checked += (s, e) => ResolutionGrid.Visibility = Visibility.Collapsed;
            FullScreenCheck.Unchecked += (s, e) => ResolutionGrid.Visibility = Visibility.Visible;

            if (_isEditing && existingConnection != null)
            {
                TitleText.Text = "Edit Connection";
                LoadConnection(existingConnection);
            }
        }

        private void LoadConnection(RdpConnection conn)
        {
            NameBox.Text = conn.Name;
            HostnameBox.Text = conn.Hostname;
            PortBox.Text = conn.Port.ToString();
            UsernameBox.Text = conn.Username;
            DomainBox.Text = conn.Domain;
            DescriptionBox.Text = conn.Description;

            // Try to select the group, or set it as text
            if (GroupCombo.Items.Contains(conn.Group))
            {
                GroupCombo.SelectedItem = conn.Group;
            }
            else
            {
                GroupCombo.Text = conn.Group;
            }

            // Load password if exists
            if (!string.IsNullOrEmpty(conn.EncryptedPassword))
            {
                try
                {
                    PasswordBox.Password = CredentialService.DecryptPassword(conn.EncryptedPassword);
                }
                catch
                {
                    // Password couldn't be decrypted - leave empty
                }
            }

            // Display settings
            FullScreenCheck.IsChecked = conn.FullScreen;
            WidthBox.Text = conn.ScreenWidth.ToString();
            HeightBox.Text = conn.ScreenHeight.ToString();
            MultiMonitorCheck.IsChecked = conn.UseMultiMonitor;
            AdminCheck.IsChecked = conn.AdminSession;
            SmartSizingCheck.IsChecked = conn.SmartSizing;
            DynamicResolutionCheck.IsChecked = conn.DynamicResolution;

            // Redirection
            ClipboardCheck.IsChecked = conn.RedirectClipboard;
            PrintersCheck.IsChecked = conn.RedirectPrinters;
            DrivesCheck.IsChecked = conn.RedirectDrives;
            SmartCardsCheck.IsChecked = conn.RedirectSmartCards;
            PortsCheck.IsChecked = conn.RedirectPorts;
            PnPDevicesCheck.IsChecked = conn.RedirectPnPDevices;

            // Audio
            AudioPlaybackCombo.SelectedIndex = conn.AudioRedirectionMode;
            AudioCaptureCheck.IsChecked = conn.AudioCaptureRedirection;

            // Performance
            DesktopCompositionCheck.IsChecked = conn.EnableDesktopComposition;
            FontSmoothingCheck.IsChecked = conn.EnableFontSmoothing;
            ThemesCheck.IsChecked = conn.EnableThemes;
            WindowDragCheck.IsChecked = conn.EnableWindowDrag;
            MenuAnimationsCheck.IsChecked = conn.EnableMenuAnimations;
            BitmapCachingCheck.IsChecked = conn.EnableBitmapCaching;
            CompressionCheck.IsChecked = conn.EnableCompression;
            NetworkAutoDetectCheck.IsChecked = conn.NetworkAutoDetect;

            // Show resolution grid if not fullscreen
            ResolutionGrid.Visibility = conn.FullScreen ? Visibility.Collapsed : Visibility.Visible;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // Validate
            if (string.IsNullOrWhiteSpace(HostnameBox.Text))
            {
                MessageBox.Show("Please enter a hostname or IP address.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                HostnameBox.Focus();
                return;
            }

            if (!int.TryParse(PortBox.Text, out int port) || port < 1 || port > 65535)
            {
                MessageBox.Show("Please enter a valid port number (1-65535).", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                PortBox.Focus();
                return;
            }

            int screenWidth = 1920;
            int screenHeight = 1080;

            if (FullScreenCheck.IsChecked != true)
            {
                if (!int.TryParse(WidthBox.Text, out screenWidth) || screenWidth < 640)
                {
                    MessageBox.Show("Please enter a valid screen width (minimum 640).", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    WidthBox.Focus();
                    return;
                }

                if (!int.TryParse(HeightBox.Text, out screenHeight) || screenHeight < 480)
                {
                    MessageBox.Show("Please enter a valid screen height (minimum 480).", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    HeightBox.Focus();
                    return;
                }
            }

            // Build connection object
            Connection = new RdpConnection
            {
                Id = _existingConnection?.Id ?? Guid.NewGuid().ToString(),
                Name = NameBox.Text.Trim(),
                Hostname = HostnameBox.Text.Trim(),
                Port = port,
                Username = UsernameBox.Text.Trim(),
                Domain = DomainBox.Text.Trim(),
                Description = DescriptionBox.Text.Trim(),
                Group = string.IsNullOrWhiteSpace(GroupCombo.Text) ? "Default" : GroupCombo.Text.Trim(),
                CreatedAt = _existingConnection?.CreatedAt ?? DateTime.Now,
                LastConnected = _existingConnection?.LastConnected,

                // Display
                FullScreen = FullScreenCheck.IsChecked == true,
                ScreenWidth = screenWidth,
                ScreenHeight = screenHeight,
                UseMultiMonitor = MultiMonitorCheck.IsChecked == true,
                AdminSession = AdminCheck.IsChecked == true,
                SmartSizing = SmartSizingCheck.IsChecked == true,
                DynamicResolution = DynamicResolutionCheck.IsChecked == true,

                // Redirection
                RedirectClipboard = ClipboardCheck.IsChecked == true,
                RedirectPrinters = PrintersCheck.IsChecked == true,
                RedirectDrives = DrivesCheck.IsChecked == true,
                RedirectSmartCards = SmartCardsCheck.IsChecked == true,
                RedirectPorts = PortsCheck.IsChecked == true,
                RedirectPnPDevices = PnPDevicesCheck.IsChecked == true,

                // Audio
                AudioRedirectionMode = AudioPlaybackCombo.SelectedIndex,
                AudioCaptureRedirection = AudioCaptureCheck.IsChecked == true,

                // Performance
                EnableDesktopComposition = DesktopCompositionCheck.IsChecked == true,
                EnableFontSmoothing = FontSmoothingCheck.IsChecked == true,
                EnableThemes = ThemesCheck.IsChecked == true,
                EnableWindowDrag = WindowDragCheck.IsChecked == true,
                EnableMenuAnimations = MenuAnimationsCheck.IsChecked == true,
                EnableBitmapCaching = BitmapCachingCheck.IsChecked == true,
                EnableCompression = CompressionCheck.IsChecked == true,
                NetworkAutoDetect = NetworkAutoDetectCheck.IsChecked == true
            };

            // Encrypt password if provided
            if (!string.IsNullOrEmpty(PasswordBox.Password))
            {
                try
                {
                    Connection.EncryptedPassword = CredentialService.EncryptPassword(PasswordBox.Password);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to encrypt password: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
