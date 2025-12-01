using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace mindvault.Converters
{
    public class TruncateConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var s = value as string ?? string.Empty;
            int max = 15;
            if (parameter != null && int.TryParse(parameter.ToString(), out var p))
            {
                max = p;
            }

            if (s.Length <= max)
                return s;

            if (max <= 3)
                return new string('.', Math.Max(0, max));

            return s.Substring(0, max) + "...";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
