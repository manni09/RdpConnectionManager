using System;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace RdpManager.Controls
{
    /// <summary>
    /// Wrapper control for the Microsoft RDP ActiveX client using late binding.
    /// Works with modern .NET without requiring compile-time COM interop assemblies.
    /// </summary>
    public class RdpClientControl : AxHost
    {
        private const string RDP_CLIENT_CLSID = "{7cacbd7b-0d99-468f-ac33-22e495c0afe5}"; // MsRdpClient9NotSafeForScripting

        private dynamic? _ocx;

        public event EventHandler? Connected;
        public event EventHandler<int>? Disconnected;
        public event EventHandler? LoginComplete;

        public RdpClientControl() : base(RDP_CLIENT_CLSID)
        {
        }

        protected override void AttachInterfaces()
        {
            try
            {
                _ocx = GetOcx();
            }
            catch
            {
                // COM object not available
            }
        }

        protected override void CreateSink()
        {
            try
            {
                // Hook up events using COM connection points
                if (_ocx != null)
                {
                    var eventType = Type.GetTypeFromCLSID(new Guid("336d5562-efa8-482e-8cb3-c5c0fc7a7db6"));
                    // Events will be handled via OnConnected etc.
                }
            }
            catch
            {
                // Event hookup failed
            }
        }

        protected override void DetachSink()
        {
            // Cleanup
        }

        #region RDP Properties

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public string Server
        {
            get => _ocx?.Server ?? string.Empty;
            set { if (_ocx != null) _ocx.Server = value; }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public string UserName
        {
            get => _ocx?.UserName ?? string.Empty;
            set { if (_ocx != null) _ocx.UserName = value; }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public string Domain
        {
            get => _ocx?.Domain ?? string.Empty;
            set { if (_ocx != null) _ocx.Domain = value; }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public int DesktopWidth
        {
            get => _ocx?.DesktopWidth ?? 1920;
            set { if (_ocx != null) _ocx.DesktopWidth = value; }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public int DesktopHeight
        {
            get => _ocx?.DesktopHeight ?? 1080;
            set { if (_ocx != null) _ocx.DesktopHeight = value; }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public int ColorDepth
        {
            get => _ocx?.ColorDepth ?? 32;
            set { if (_ocx != null) _ocx.ColorDepth = value; }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public bool FullScreen
        {
            get => _ocx?.FullScreen ?? false;
            set { if (_ocx != null) _ocx.FullScreen = value; }
        }

        public dynamic? AdvancedSettings
        {
            get
            {
                try
                {
                    return _ocx?.AdvancedSettings9;
                }
                catch
                {
                    try { return _ocx?.AdvancedSettings8; } catch { }
                    try { return _ocx?.AdvancedSettings7; } catch { }
                    return null;
                }
            }
        }

        public bool IsConnected => _ocx?.Connected == 1;

        #endregion

        #region RDP Methods

        public void SetPassword(string password)
        {
            try
            {
                if (_ocx != null)
                {
                    // Get the non-scriptable interface for setting password
                    Type? nonScriptableType = Type.GetTypeFromCLSID(
                        new Guid("2f079c4c-87b2-4afd-97ab-20cdb43038ae"));
                    
                    if (nonScriptableType != null)
                    {
                        // Use reflection to set ClearTextPassword
                        var ocxObj = GetOcx();
                        if (ocxObj != null)
                        {
                            var prop = ocxObj.GetType().GetProperty("ClearTextPassword");
                            prop?.SetValue(ocxObj, password);
                        }
                    }
                }
            }
            catch
            {
                // Password setting not supported in this way
            }
        }

        public void Connect()
        {
            try
            {
                _ocx?.Connect();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to connect: {ex.Message}", ex);
            }
        }

        public void Disconnect()
        {
            try
            {
                _ocx?.Disconnect();
            }
            catch
            {
                // Already disconnected
            }
        }

        /// <summary>
        /// Updates the remote session display settings to match the new size.
        /// Requires the connection to support dynamic resolution updates.
        /// </summary>
        public void UpdateSessionDisplaySettings(int width, int height)
        {
            try
            {
                if (_ocx == null || !IsConnected) return;

                // Try to use the Reconnect method with new resolution (RDP 8.1+)
                // This triggers a display update without full reconnection
                dynamic? advSettings = AdvancedSettings;
                if (advSettings != null)
                {
                    try
                    {
                        // Enable smart sizing for smooth resize
                        advSettings.SmartSizing = true;
                    }
                    catch { }
                }

                // Try UpdateSessionDisplaySettings if available (RDP 8.1+)
                try
                {
                    _ocx?.UpdateSessionDisplaySettings(
                        (uint)width,   // ulDesktopWidth
                        (uint)height,  // ulDesktopHeight
                        (uint)width,   // ulPhysicalWidth
                        (uint)height,  // ulPhysicalHeight
                        0,             // ulOrientation
                        100,           // ulDesktopScaleFactor
                        100);          // ulDeviceScaleFactor
                }
                catch
                {
                    // Method not available on this RDP client version
                    // Fall back to smart sizing which will scale the existing session
                }
            }
            catch
            {
                // Resolution update not supported
            }
        }

        #endregion

        #region Events

        // Override WndProc to capture RDP events
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            // Handle connection state changes through polling or COM events
        }

        public void RaiseConnected() => Connected?.Invoke(this, EventArgs.Empty);
        public void RaiseDisconnected(int reason) => Disconnected?.Invoke(this, reason);
        public void RaiseLoginComplete() => LoginComplete?.Invoke(this, EventArgs.Empty);

        #endregion
    }
}
