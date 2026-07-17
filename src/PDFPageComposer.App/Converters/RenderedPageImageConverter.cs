using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PDFPageComposer.App.Models;

namespace PDFPageComposer.App.Converters;

public sealed class RenderedPageImageConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not RenderedPageImage image || image.Bgra32Pixels.Length == 0)
        {
            return null;
        }

        var bitmap = BitmapSource.Create(
            image.PixelWidth,
            image.PixelHeight,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            image.Bgra32Pixels,
            image.Stride);
        bitmap.Freeze();
        return bitmap;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
