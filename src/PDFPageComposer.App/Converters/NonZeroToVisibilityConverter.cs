using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PDFPageComposer.App.Converters;

/// <summary>Visible for a non-zero number, collapsed for 0 or a non-number.</summary>
public sealed class NonZeroToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is int number && number != 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
