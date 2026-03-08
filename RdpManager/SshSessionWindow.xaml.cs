using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Renci.SshNet;
using RdpManager.Models;
using RdpManager.Services;

namespace RdpManager
{
    public partial class SshSessionWindow : Window
    {
        private RdpConnection? _connection;
        private SshClient? _sshClient;
        private ShellStream? _shellStream;
        private CancellationTokenSource? _readCancellation;
        private DateTime _sessionStartTime;
        private DispatcherTimer? _durationTimer;
        private bool _isConnected;
        private StringBuilder _inputBuffer = new();
        
        // ANSI color mapping
        private static readonly Dictionary<int, Color> AnsiColors = new()
        {
            { 0, Color.FromRgb(0, 0, 0) },       // Black
            { 1, Color.FromRgb(205, 49, 49) },   // Red
            { 2, Color.FromRgb(13, 188, 121) },  // Green
            { 3, Color.FromRgb(229, 229, 16) },  // Yellow
            { 4, Color.FromRgb(36, 114, 200) },  // Blue
            { 5, Color.FromRgb(188, 63, 188) },  // Magenta
            { 6, Color.FromRgb(17, 168, 205) },  // Cyan
            { 7, Color.FromRgb(229, 229, 229) }, // White
            { 8, Color.FromRgb(102, 102, 102) }, // Bright Black
            { 9, Color.FromRgb(241, 76, 76) },   // Bright Red
            { 10, Color.FromRgb(35, 209, 139) }, // Bright Green
            { 11, Color.FromRgb(245, 245, 67) }, // Bright Yellow
            { 12, Color.FromRgb(59, 142, 234) }, // Bright Blue
            { 13, Color.FromRgb(214, 112, 214) },// Bright Magenta
            { 14, Color.FromRgb(41, 184, 219) }, // Bright Cyan
            { 15, Color.FromRgb(255, 255, 255) } // Bright White
        };

        public SshSessionWindow()
        {
            LoadSavedWindowState();
            InitializeComponent();
        }

        private void LoadSavedWindowState()
        {
            try
            {
                var settings = StorageService.LoadSettings();
                var state = settings.SshSessionWindowState;
                
                if (state != null)
                {
                    if (!double.IsNaN(state.Left) && !double.IsNaN(state.Top))
                    {
                        WindowStartupLocation = WindowStartupLocation.Manual;
                        Left = state.Left;
                        Top = state.Top;
                    }
                    
                    Width = state.Width > 0 ? state.Width : 900;
                    Height = state.Height > 0 ? state.Height : 600;
                    
                    if (state.IsMaximized)
                    {
                        WindowState = WindowState.Maximized;
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Error("Failed to load SSH window state", ex);
            }
        }

        private void SaveWindowState()
        {
            try
            {
                var settings = StorageService.LoadSettings();
                
                settings.SshSessionWindowState = new WindowSettings
                {
                    Left = RestoreBounds.Left,
                    Top = RestoreBounds.Top,
                    Width = RestoreBounds.Width,
                    Height = RestoreBounds.Height,
                    IsMaximized = WindowState == WindowState.Maximized
                };
                
                StorageService.SaveSettings(settings);
            }
            catch (Exception ex)
            {
                LoggingService.Error("Failed to save SSH window state", ex);
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            TerminalOutput.Focus();
        }

        public async void Connect(RdpConnection connection)
        {
            _connection = connection;
            
            Title = $"SSH - {connection.DisplayName}";
            ConnectionTitle.Text = connection.DisplayName;
            ConnectionInfo.Text = $"{connection.Username}@{connection.ConnectionString}";
            
            UpdateStatus("Connecting...", Brushes.Orange);
            
            // Register with session manager
            SessionManagerService.RegisterSession(connection.Id, this);
            
            await Task.Run(() => ConnectSsh());
        }

        private void ConnectSsh()
        {
            if (_connection == null) return;

            try
            {
                var connectionInfo = CreateConnectionInfo();
                
                _sshClient = new SshClient(connectionInfo);
                _sshClient.ConnectionInfo.Timeout = TimeSpan.FromSeconds(30);
                
                LoggingService.Info($"Connecting to SSH: {_connection.ConnectionString}");
                _sshClient.Connect();
                
                if (!_sshClient.IsConnected)
                {
                    throw new Exception("Failed to establish SSH connection");
                }

                // Create shell stream with terminal emulation
                _shellStream = _sshClient.CreateShellStream(
                    "xterm-256color",  // Terminal type
                    80,                 // Columns
                    24,                 // Rows  
                    800,                // Width in pixels
                    600,                // Height in pixels
                    4096                // Buffer size
                );

                _isConnected = true;
                _sessionStartTime = DateTime.Now;
                
                Dispatcher.Invoke(() =>
                {
                    UpdateStatus("Connected", Brushes.LimeGreen);
                    StartDurationTimer();
                    TerminalOutput.Focus();
                });

                // Start reading output
                _readCancellation = new CancellationTokenSource();
                Task.Run(() => ReadOutputLoop(_readCancellation.Token));
                
                LoggingService.Info($"SSH connected to {_connection.ConnectionString}");
            }
            catch (Exception ex)
            {
                LoggingService.Error($"SSH connection failed: {ex.Message}", ex);
                
                Dispatcher.Invoke(() =>
                {
                    UpdateStatus("Connection Failed", Brushes.Red);
                    AppendText($"\r\n*** Connection failed: {ex.Message} ***\r\n", Colors.Red);
                    
                    MessageBox.Show($"Failed to connect:\n{ex.Message}", "SSH Connection Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

        private ConnectionInfo CreateConnectionInfo()
        {
            if (_connection == null) throw new InvalidOperationException("No connection configured");

            var authMethods = new List<AuthenticationMethod>();
            
            // Password authentication
            if (!string.IsNullOrEmpty(_connection.EncryptedPassword))
            {
                try
                {
                    var password = CredentialService.DecryptPassword(_connection.EncryptedPassword);
                    authMethods.Add(new PasswordAuthenticationMethod(_connection.Username, password));
                }
                catch (Exception ex)
                {
                    LoggingService.Error("Failed to decrypt password for SSH", ex);
                }
            }

            // Private key authentication
            if (_connection.SshAuthMethod != SshAuthMethod.Password && 
                !string.IsNullOrEmpty(_connection.SshPrivateKeyPath) &&
                File.Exists(_connection.SshPrivateKeyPath))
            {
                try
                {
                    PrivateKeyFile keyFile;
                    
                    if (!string.IsNullOrEmpty(_connection.EncryptedSshPassphrase))
                    {
                        var passphrase = CredentialService.DecryptPassword(_connection.EncryptedSshPassphrase);
                        keyFile = new PrivateKeyFile(_connection.SshPrivateKeyPath, passphrase);
                    }
                    else
                    {
                        keyFile = new PrivateKeyFile(_connection.SshPrivateKeyPath);
                    }
                    
                    authMethods.Add(new PrivateKeyAuthenticationMethod(_connection.Username, keyFile));
                }
                catch (Exception ex)
                {
                    LoggingService.Error($"Failed to load private key: {ex.Message}", ex);
                }
            }

            if (authMethods.Count == 0)
            {
                throw new Exception("No valid authentication method configured. Please provide a password or private key.");
            }

            return new ConnectionInfo(
                _connection.Hostname,
                _connection.Port,
                _connection.Username,
                authMethods.ToArray()
            );
        }

        private async Task ReadOutputLoop(CancellationToken cancellationToken)
        {
            var buffer = new byte[4096];
            
            while (!cancellationToken.IsCancellationRequested && _isConnected && _shellStream != null)
            {
                try
                {
                    if (_shellStream.DataAvailable)
                    {
                        int bytesRead = await _shellStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                        
                        if (bytesRead > 0)
                        {
                            string output = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                            Dispatcher.Invoke(() => ProcessAnsiOutput(output));
                        }
                    }
                    else
                    {
                        await Task.Delay(10, cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (_isConnected)
                    {
                        LoggingService.Error($"Error reading SSH output: {ex.Message}", ex);
                        Dispatcher.Invoke(() =>
                        {
                            AppendText($"\r\n*** Connection lost: {ex.Message} ***\r\n", Colors.Red);
                            HandleDisconnect();
                        });
                    }
                    break;
                }
            }
        }

        private void ProcessAnsiOutput(string output)
        {
            // Simple ANSI escape code parser
            int i = 0;
            var currentText = new StringBuilder();
            Color currentColor = Colors.LightGray;
            bool isBold = false;

            while (i < output.Length)
            {
                if (output[i] == '\x1b' && i + 1 < output.Length && output[i + 1] == '[')
                {
                    // Flush current text
                    if (currentText.Length > 0)
                    {
                        AppendText(currentText.ToString(), currentColor, isBold);
                        currentText.Clear();
                    }

                    // Parse escape sequence
                    i += 2; // Skip ESC[
                    var escapeSeq = new StringBuilder();
                    
                    while (i < output.Length && !char.IsLetter(output[i]))
                    {
                        escapeSeq.Append(output[i]);
                        i++;
                    }
                    
                    if (i < output.Length)
                    {
                        char command = output[i];
                        i++;
                        
                        if (command == 'm') // SGR - Select Graphic Rendition
                        {
                            var codes = escapeSeq.ToString().Split(';');
                            foreach (var codeStr in codes)
                            {
                                if (int.TryParse(codeStr, out int code))
                                {
                                    if (code == 0)
                                    {
                                        currentColor = Colors.LightGray;
                                        isBold = false;
                                    }
                                    else if (code == 1)
                                    {
                                        isBold = true;
                                    }
                                    else if (code >= 30 && code <= 37)
                                    {
                                        int colorIndex = code - 30 + (isBold ? 8 : 0);
                                        if (AnsiColors.TryGetValue(colorIndex, out var color))
                                            currentColor = color;
                                    }
                                    else if (code >= 90 && code <= 97)
                                    {
                                        int colorIndex = code - 90 + 8;
                                        if (AnsiColors.TryGetValue(colorIndex, out var color))
                                            currentColor = color;
                                    }
                                }
                            }
                        }
                        // Ignore other escape sequences (cursor movement, etc.)
                    }
                }
                else if (output[i] == '\r')
                {
                    // Carriage return - flush and handle
                    if (currentText.Length > 0)
                    {
                        AppendText(currentText.ToString(), currentColor, isBold);
                        currentText.Clear();
                    }
                    i++;
                }
                else if (output[i] == '\b')
                {
                    // Backspace
                    if (currentText.Length > 0)
                    {
                        currentText.Length--;
                    }
                    i++;
                }
                else
                {
                    currentText.Append(output[i]);
                    i++;
                }
            }

            // Flush remaining text
            if (currentText.Length > 0)
            {
                AppendText(currentText.ToString(), currentColor, isBold);
            }

            // Scroll to end
            TerminalScroller.ScrollToEnd();
        }

        private void AppendText(string text, Color color, bool isBold = false)
        {
            var paragraph = TerminalOutput.Document.Blocks.LastBlock as Paragraph;
            
            if (paragraph == null)
            {
                paragraph = new Paragraph();
                TerminalOutput.Document.Blocks.Add(paragraph);
            }

            var run = new Run(text)
            {
                Foreground = new SolidColorBrush(color)
            };
            
            if (isBold)
            {
                run.FontWeight = FontWeights.Bold;
            }

            paragraph.Inlines.Add(run);
            
            // Move caret to end
            TerminalOutput.CaretPosition = TerminalOutput.Document.ContentEnd;
        }

        private void StartDurationTimer()
        {
            _durationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            
            _durationTimer.Tick += (s, e) =>
            {
                var duration = DateTime.Now - _sessionStartTime;
                SessionDuration.Text = duration.ToString(@"hh\:mm\:ss");
            };
            
            _durationTimer.Start();
        }

        private void UpdateStatus(string status, Brush color)
        {
            StatusText.Text = status;
            StatusIndicator.Background = color;
        }

        private void TerminalOutput_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!_isConnected || _shellStream == null) return;

            string? dataToSend = null;

            switch (e.Key)
            {
                case Key.Enter:
                    dataToSend = "\n";
                    break;
                case Key.Back:
                    dataToSend = "\b";
                    break;
                case Key.Tab:
                    dataToSend = "\t";
                    break;
                case Key.Escape:
                    dataToSend = "\x1b";
                    break;
                case Key.Up:
                    dataToSend = "\x1b[A";
                    break;
                case Key.Down:
                    dataToSend = "\x1b[B";
                    break;
                case Key.Right:
                    dataToSend = "\x1b[C";
                    break;
                case Key.Left:
                    dataToSend = "\x1b[D";
                    break;
                case Key.Home:
                    dataToSend = "\x1b[H";
                    break;
                case Key.End:
                    dataToSend = "\x1b[F";
                    break;
                case Key.Delete:
                    dataToSend = "\x1b[3~";
                    break;
                case Key.PageUp:
                    dataToSend = "\x1b[5~";
                    break;
                case Key.PageDown:
                    dataToSend = "\x1b[6~";
                    break;
                case Key.C:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        dataToSend = "\x03"; // Ctrl+C
                    }
                    break;
                case Key.D:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        dataToSend = "\x04"; // Ctrl+D (EOF)
                    }
                    break;
                case Key.Z:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        dataToSend = "\x1a"; // Ctrl+Z
                    }
                    break;
                case Key.L:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        dataToSend = "\x0c"; // Ctrl+L (clear)
                    }
                    break;
            }

            if (dataToSend != null)
            {
                SendToShell(dataToSend);
                e.Handled = true;
            }
        }

        private void TerminalOutput_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!_isConnected || _shellStream == null) return;

            SendToShell(e.Text);
            e.Handled = true;
        }

        private void SendToShell(string data)
        {
            if (_shellStream == null || !_isConnected) return;

            try
            {
                _shellStream.Write(data);
                _shellStream.Flush();
            }
            catch (Exception ex)
            {
                LoggingService.Error($"Failed to send data to SSH: {ex.Message}", ex);
                AppendText($"\r\n*** Error sending data: {ex.Message} ***\r\n", Colors.Red);
            }
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            var selection = TerminalOutput.Selection;
            if (!selection.IsEmpty)
            {
                Clipboard.SetText(selection.Text);
            }
        }

        private void Paste_Click(object sender, RoutedEventArgs e)
        {
            if (Clipboard.ContainsText() && _isConnected && _shellStream != null)
            {
                string text = Clipboard.GetText();
                SendToShell(text);
            }
        }

        private void Disconnect_Click(object sender, RoutedEventArgs e)
        {
            Disconnect();
        }

        private void Disconnect()
        {
            if (!_isConnected) return;

            _isConnected = false;
            _readCancellation?.Cancel();
            _durationTimer?.Stop();

            try
            {
                _shellStream?.Close();
                _shellStream?.Dispose();
                _sshClient?.Disconnect();
                _sshClient?.Dispose();
            }
            catch (Exception ex)
            {
                LoggingService.Error($"Error during SSH disconnect: {ex.Message}", ex);
            }

            _shellStream = null;
            _sshClient = null;

            HandleDisconnect();
        }

        private void HandleDisconnect()
        {
            UpdateStatus("Disconnected", Brushes.Gray);
            AppendText("\r\n*** Session ended ***\r\n", Colors.Yellow);
            
            if (_connection != null)
            {
                SessionManagerService.UnregisterSession(_connection.Id);
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveWindowState();
            Disconnect();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            // Handle Ctrl+Shift+V for paste
            if (e.Key == Key.V && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                Paste_Click(sender, e);
                e.Handled = true;
            }
        }

        private void TerminalScroller_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Default scroll behavior
        }
    }
}
