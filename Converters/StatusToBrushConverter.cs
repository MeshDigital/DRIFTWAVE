using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using SLSKDONET.Models;

namespace SLSKDONET.Converters;

/// <summary>
/// Converts DownloadState to SolidColorBrush for status display in UI.
/// </summary>
public class StatusToBrushConverter : IValueConverter
{
    // Color palette for status states
    private static readonly System.Windows.Media.Color CompletedColor = System.Windows.Media.Color.FromArgb(255, 76, 175, 80);     // Green
    private static readonly System.Windows.Media.Color FailedColor = System.Windows.Media.Color.FromArgb(255, 244, 67, 54);        // Red
    private static readonly System.Windows.Media.Color CancelledColor = System.Windows.Media.Color.FromArgb(255, 255, 152, 0);     // Orange
    private static readonly System.Windows.Media.Color DownloadingColor = System.Windows.Media.Color.FromArgb(255, 33, 150, 243);  // Blue
    private static readonly System.Windows.Media.Color RetryingColor = System.Windows.Media.Color.FromArgb(255, 255, 193, 7);      // Amber/Yellow
    private static readonly System.Windows.Media.Color SearchingColor = System.Windows.Media.Color.FromArgb(255, 156, 39, 176);    // Purple
    private static readonly System.Windows.Media.Color PendingColor = System.Windows.Media.Color.FromArgb(255, 158, 158, 158);     // Grey

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo? culture)
    {
        if (value is not DownloadState state)
            return new SolidColorBrush(PendingColor);

        var color = state switch
        {
            DownloadState.Completed => CompletedColor,
            DownloadState.Failed => FailedColor,
            DownloadState.Cancelled => CancelledColor,
            DownloadState.Downloading => DownloadingColor,
            DownloadState.Retrying => RetryingColor,
            DownloadState.Searching => SearchingColor,
            DownloadState.Pending => PendingColor,
            _ => PendingColor
        };

        return new SolidColorBrush(color);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo? culture)
    {
        throw new NotSupportedException("StatusToBrushConverter does not support reverse conversion.");
    }
}
