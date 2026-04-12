using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Client.Converters
{
    public class BoolToWidthConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isExpanded)
            {
                return isExpanded ? 250.0 : 5.0;
            }
            return 250.0;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is double width)
            {
                return width > 100;
            }
            return true;
        }
    }
}
