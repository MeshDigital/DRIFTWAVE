using System.Globalization;
using System.Windows.Data;

namespace SLSKDONET.Converters;

/// <summary>
/// Converts LibraryViewMode enum to boolean for toggle button states.
/// </summary>
public class ViewModeToggleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo? culture)
    {
        if (value is not SLSKDONET.Views.LibraryViewMode currentMode || parameter is not string targetMode)
            return false;

        return currentMode.ToString() == targetMode;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo? culture)
    {
        throw new NotSupportedException("ViewModeToggleConverter does not support reverse conversion.");
    }
}

/// <summary>
/// Converts LibraryViewMode enum to Visibility for showing/hiding templates.
/// </summary>
public class ViewModeToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo? culture)
    {
        if (value is not SLSKDONET.Views.LibraryViewMode currentMode || parameter is not string targetMode)
            return System.Windows.Visibility.Collapsed;

        return currentMode.ToString() == targetMode 
            ? System.Windows.Visibility.Visible 
            : System.Windows.Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo? culture)
    {
        throw new NotSupportedException("ViewModeToVisibilityConverter does not support reverse conversion.");
    }
}
