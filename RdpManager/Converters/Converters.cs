using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using RdpManager.Models;

namespace RdpManager.Converters
{
    /// <summary>
    /// Converts a boolean to Visibility (true = Visible, false = Collapsed)
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                return visibility == Visibility.Visible;
            }
            return false;
        }
    }

    /// <summary>
    /// Shows "(set)" or "(not set)" based on whether the password string is empty
    /// </summary>
    public class PasswordSetConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str && !string.IsNullOrEmpty(str))
            {
                return "••••••••";
            }
            return "(not set)";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Inverts a boolean value
    /// </summary>
    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return false;
        }
    }

    /// <summary>
    /// Hides the group label when it's "Default" (not informative).
    /// Returns Collapsed for "Default", Visible otherwise.
    /// </summary>
    public class HideDefaultGroupConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string group && !string.IsNullOrEmpty(group) && 
                !group.Equals("Default", StringComparison.OrdinalIgnoreCase))
            {
                return Visibility.Visible;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts a connection ID to Visibility based on whether it has an active session.
    /// Returns Visible if there's an active session, Collapsed otherwise.
    /// </summary>
    public class ActiveSessionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string connectionId)
            {
                return Services.SessionManagerService.HasActiveSession(connectionId) 
                    ? Visibility.Visible 
                    : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts ConnectionType to a brush color.
    /// RDP = Blue, SSH = Green
    /// </summary>
    public class ConnectionTypeBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush RdpBrush = new(Color.FromRgb(0, 120, 212)); // Blue
        private static readonly SolidColorBrush SshBrush = new(Color.FromRgb(16, 124, 16)); // Green

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ConnectionType connType)
            {
                return connType == ConnectionType.SSH ? SshBrush : RdpBrush;
            }
            return RdpBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
