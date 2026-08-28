using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Mdv.Windows.App.Converters;

public sealed class TocIndentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var level = value is int levelValue ? levelValue : 1;
        return new Thickness(Math.Max(0, (level - 1) * 16), 2, 2, 2);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
