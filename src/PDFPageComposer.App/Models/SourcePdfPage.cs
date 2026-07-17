using CommunityToolkit.Mvvm.ComponentModel;

namespace PDFPageComposer.App.Models;

public sealed partial class SourcePdfPage : ObservableObject
{
    public SourcePdfPage(Guid id, Guid sourceFileId, int pageNumber)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageNumber);

        Id = id;
        SourceFileId = sourceFileId;
        PageNumber = pageNumber;
    }

    public Guid Id { get; }

    public Guid SourceFileId { get; }

    public int PageNumber { get; }

    public double Width { get; init; }

    public double Height { get; init; }

    public int Rotation { get; init; }

    [ObservableProperty]
    private bool isSelected;

    [ObservableProperty]
    private ThumbnailState thumbnailState = ThumbnailState.NotRequested;

    [ObservableProperty]
    private int outputOccurrenceCount;

    [ObservableProperty]
    private byte[]? thumbnailPixels;

    [ObservableProperty]
    private int thumbnailPixelWidth;

    [ObservableProperty]
    private int thumbnailPixelHeight;

    [ObservableProperty]
    private int thumbnailStride;

    [ObservableProperty]
    private string? thumbnailError;

    public bool HasThumbnail => ThumbnailPixels is { Length: > 0 } && ThumbnailPixelWidth > 0 && ThumbnailPixelHeight > 0;

    public void SetThumbnail(int pixelWidth, int pixelHeight, int stride, byte[] pixels)
    {
        ThumbnailPixelWidth = pixelWidth;
        ThumbnailPixelHeight = pixelHeight;
        ThumbnailStride = stride;
        ThumbnailPixels = pixels;
        ThumbnailError = null;
        ThumbnailState = ThumbnailState.Ready;
        OnPropertyChanged(nameof(HasThumbnail));
    }

    public void SetThumbnailError(string message)
    {
        ThumbnailPixels = null;
        ThumbnailPixelWidth = 0;
        ThumbnailPixelHeight = 0;
        ThumbnailStride = 0;
        ThumbnailError = message;
        ThumbnailState = ThumbnailState.Error;
        OnPropertyChanged(nameof(HasThumbnail));
    }
}
