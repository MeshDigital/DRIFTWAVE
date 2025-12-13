using System;
using System.Globalization;
using System.Windows.Data;

namespace SLSKDONET.Converters
{
    public class BytesToReadableStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is long bytes)
            {
                return FormatBytes(bytes);
            }
            if (value is int iBytes)
            {
                return FormatBytes(iBytes);
            }
            return "0 B";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;
            
            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }
            
            if (counter >= suffixes.Length) counter = suffixes.Length - 1;
            
            return string.Format("{0:n1} {1}", number, suffixes[counter]);
        }
    }
}
