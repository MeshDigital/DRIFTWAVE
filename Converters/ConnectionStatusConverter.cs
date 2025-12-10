using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SLSKDONET.Converters;

/// <summary>
/// Converts boolean IsConnected to a color brush for status indicator.
/// </summary>
public class ConnectionStatusColorConverter : IValueConverter
{
    private static readonly System.Windows.Media.Color ConnectedColor = System.Windows.Media.Color.FromArgb(255, 76, 175, 80);     // Green
    private static readonly System.Windows.Media.Color DisconnectedColor = System.Windows.Media.Color.FromArgb(255, 244, 67, 54);   // Red
    private static readonly System.Windows.Media.Color ReconnectingColor = System.Windows.Media.Color.FromArgb(255, 255, 193, 7);    // Amber

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo? culture)
    {
        if (value is bool isConnected)
        {
            var color = isConnected ? ConnectedColor : DisconnectedColor;
            return new SolidColorBrush(color);
        }

        return new SolidColorBrush(DisconnectedColor);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo? culture)
    {
        throw new NotSupportedException("ConnectionStatusColorConverter does not support reverse conversion.");
    }
}

/// <summary>
/// Converts boolean IsConnected to connection status text.
/// </summary>
public class ConnectionStatusTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo? culture)
    {
        if (value is bool isConnected)
        {
            return isConnected ? "🟢 Connected to Soulseek" : "🔴 Disconnected";
        }

        return "🔴 Disconnected";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo? culture)
    {
        throw new NotSupportedException("ConnectionStatusTextConverter does not support reverse conversion.");
    }
}
