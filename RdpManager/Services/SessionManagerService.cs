using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using RdpManager.Models;

namespace RdpManager.Services
{
    /// <summary>
    /// Manages active RDP and SSH sessions and tracks which connections have open windows.
    /// </summary>
    public static class SessionManagerService
    {
        private static readonly Dictionary<string, Window> _activeSessions = new();
        
        /// <summary>
        /// Event fired when the active sessions change.
        /// </summary>
        public static event EventHandler? SessionsChanged;

        /// <summary>
        /// Registers an active session for a connection.
        /// </summary>
        public static void RegisterSession(string connectionId, Window window)
        {
            if (string.IsNullOrEmpty(connectionId)) return;
            
            lock (_activeSessions)
            {
                _activeSessions[connectionId] = window;
            }
            
            LoggingService.Debug($"Session registered for connection {connectionId}");
            SessionsChanged?.Invoke(null, EventArgs.Empty);
        }

        /// <summary>
        /// Unregisters a session when it closes.
        /// </summary>
        public static void UnregisterSession(string connectionId)
        {
            if (string.IsNullOrEmpty(connectionId)) return;
            
            lock (_activeSessions)
            {
                _activeSessions.Remove(connectionId);
            }
            
            LoggingService.Debug($"Session unregistered for connection {connectionId}");
            SessionsChanged?.Invoke(null, EventArgs.Empty);
        }

        /// <summary>
        /// Checks if a connection has an active session.
        /// </summary>
        public static bool HasActiveSession(string connectionId)
        {
            if (string.IsNullOrEmpty(connectionId)) return false;
            
            lock (_activeSessions)
            {
                return _activeSessions.ContainsKey(connectionId);
            }
        }

        /// <summary>
        /// Gets the active session window for a connection, if one exists.
        /// </summary>
        public static Window? GetActiveSession(string connectionId)
        {
            if (string.IsNullOrEmpty(connectionId)) return null;
            
            lock (_activeSessions)
            {
                return _activeSessions.TryGetValue(connectionId, out var window) ? window : null;
            }
        }

        /// <summary>
        /// Gets all connection IDs with active sessions.
        /// </summary>
        public static IReadOnlyList<string> GetActiveConnectionIds()
        {
            lock (_activeSessions)
            {
                return _activeSessions.Keys.ToList();
            }
        }

        /// <summary>
        /// Gets the count of active sessions.
        /// </summary>
        public static int ActiveSessionCount
        {
            get
            {
                lock (_activeSessions)
                {
                    return _activeSessions.Count;
                }
            }
        }

        /// <summary>
        /// Brings an existing session window to the foreground.
        /// </summary>
        public static bool FocusSession(string connectionId)
        {
            var window = GetActiveSession(connectionId);
            if (window == null) return false;
            
            try
            {
                if (window.WindowState == WindowState.Minimized)
                {
                    window.WindowState = WindowState.Normal;
                }
                
                window.Activate();
                window.Focus();
                
                LoggingService.Debug($"Focused existing session for connection {connectionId}");
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.Warn($"Could not focus session window: {ex.Message}");
                return false;
            }
        }
    }
}
