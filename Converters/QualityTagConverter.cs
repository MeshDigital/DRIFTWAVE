using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using SLSKDONET.Models;

namespace SLSKDONET.Converters;

/// <summary>
/// Converts Track properties to quality indicator tags (FLAC, 320kbps, etc.)
/// Returns a formatted string with quality badges based on bitrate and format.
/// </summary>
public class QualityTagConverter : IMultiValueConverter
{
    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo? culture)
    {
        if (values.Length < 2 || values[0] is not int bitrate)
            return "";

        var tags = new List<string>();

        // Detect format from filename if available
        var filename = values.Length > 1 ? values[1]?.ToString() ?? "" : "";
        
        // Check for FLAC
        if (filename.EndsWith(".flac", StringComparison.OrdinalIgnoreCase))
        {
            tags.Add("FLAC");
        }
        
        // Check for high bitrate (320kbps MP3 or higher)
        if (bitrate >= 320)
        {
            tags.Add($"{bitrate}");
        }
        
        // Check for very high bitrate lossless
        if (bitrate >= 1000)
        {
            tags.Add("HQ");
        }

        return string.Join(" ", tags);
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo? culture)
    {
        throw new NotSupportedException("QualityTagConverter does not support reverse conversion.");
    }
}

/// <summary>
/// Converts Track bitrate to a background color brush for visual quality indication.
/// </summary>
public class BitrateToQualityColorConverter : IValueConverter
{
    // Color palette for quality tiers
    private static readonly System.Windows.Media.Color FlacColor = System.Windows.Media.Color.FromArgb(200, 76, 175, 80);      // Green - FLAC
    private static readonly System.Windows.Media.Color HighBitrateColor = System.Windows.Media.Color.FromArgb(200, 33, 150, 243); // Blue - 320kbps+
    private static readonly System.Windows.Media.Color MediumBitrateColor = System.Windows.Media.Color.FromArgb(200, 255, 193, 7);  // Amber - 192-319
    private static readonly System.Windows.Media.Color LowBitrateColor = System.Windows.Media.Color.FromArgb(200, 244, 67, 54);     // Red - <192

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo? culture)
    {
        if (value is not int bitrate)
            return new SolidColorBrush(System.Windows.Media.Colors.Transparent);

        // If bitrate is very high (>500), likely lossless (FLAC)
        if (bitrate >= 500)
        {
            return new SolidColorBrush(FlacColor);
        }

        // 320kbps and above
        if (bitrate >= 320)
        {
            return new SolidColorBrush(HighBitrateColor);
        }

        // 192-319 kbps
        if (bitrate >= 192)
        {
            return new SolidColorBrush(MediumBitrateColor);
        }

        // Below 192 kbps
        return new SolidColorBrush(LowBitrateColor);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo? culture)
    {
        throw new NotSupportedException("BitrateToQualityColorConverter does not support reverse conversion.");
    }
}

/// <summary>
/// Converts Track filename to format tag (FLAC, MP3, etc.)
/// </summary>
public class FormatTagConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo? culture)
    {
        if (value is not string filename)
            return "";

        if (filename.EndsWith(".flac", StringComparison.OrdinalIgnoreCase))
            return "FLAC";
        if (filename.EndsWith(".m4a", StringComparison.OrdinalIgnoreCase) || filename.EndsWith(".aac", StringComparison.OrdinalIgnoreCase))
            return "AAC";
        if (filename.EndsWith(".opus", StringComparison.OrdinalIgnoreCase))
            return "Opus";
        if (filename.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
            return "OGG";
        if (filename.EndsWith(".wma", StringComparison.OrdinalIgnoreCase))
            return "WMA";
        if (filename.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
            return "WAV";
        
        // Default to MP3
        return "MP3";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo? culture)
    {
        throw new NotSupportedException("FormatTagConverter does not support reverse conversion.");
    }
}
