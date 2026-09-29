using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace ErmayMuhasebe.Avalonia.Converters
{
    public class TabMatchConverter : IValueConverter
    {
        public static readonly TabMatchConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string activeTab && parameter is string targetTab)
            {
                return string.Equals(activeTab, targetTab, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ActiveTabBrushConverter : IValueConverter
    {
        public static readonly ActiveTabBrushConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string activeTab && parameter is string targetTab)
            {
                bool isMatch = string.Equals(activeTab, targetTab, StringComparison.OrdinalIgnoreCase);
                if (isMatch)
                {
                    // Aktif sekme: Elevated panel tonu
                    return new SolidColorBrush(Color.Parse("#22242A"));
                }
            }
            // Pasif sekme: Şeffaf (segment çubuğunun zeminini temiz gösterir)
            return new SolidColorBrush(Colors.Transparent);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ActiveTabBorderBrushConverter : IValueConverter
    {
        public static readonly ActiveTabBorderBrushConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string activeTab && parameter is string targetTab)
            {
                bool isMatch = string.Equals(activeTab, targetTab, StringComparison.OrdinalIgnoreCase);
                if (isMatch)
                {
                    // Aktif sekme: İnce parlak mavi/kenarlık vurgusu
                    return new SolidColorBrush(Color.Parse("#4060A5FA"));
                }
            }
            return new SolidColorBrush(Colors.Transparent);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
