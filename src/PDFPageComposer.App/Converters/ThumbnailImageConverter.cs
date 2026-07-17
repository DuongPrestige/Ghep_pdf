using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PDFPageComposer.App.Models;

namespace PDFPageComposer.App.Converters;

public sealed class ThumbnailImageConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not SourcePdfPage page || !page.HasThumbnail || page.ThumbnailPixels is null)
        {
            return null;
        }

        var bitmap = BitmapSource.Create(
            page.ThumbnailPixelWidth,
            page.ThumbnailPixelHeight,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            page.ThumbnailPixels,
            page.ThumbnailStride);
        bitmap.Freeze();
        return bitmap;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
