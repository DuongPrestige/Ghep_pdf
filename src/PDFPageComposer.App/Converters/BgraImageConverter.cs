using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PDFPageComposer.App.Converters;

public sealed class BgraImageConverter : IMultiValueConverter
{
    public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 4 ||
            values[0] is not byte[] { Length: > 0 } pixels ||
            values[1] is not int pixelWidth ||
            values[2] is not int pixelHeight ||
            values[3] is not int stride ||
            pixelWidth <= 0 ||
            pixelHeight <= 0 ||
            stride <= 0)
        {
            return null;
        }

        var bitmap = BitmapSource.Create(
            pixelWidth,
            pixelHeight,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            pixels,
            stride);
        bitmap.Freeze();
        return bitmap;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
