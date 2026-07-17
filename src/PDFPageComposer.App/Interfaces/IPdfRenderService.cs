namespace PDFPageComposer.App.Interfaces;

public interface IPdfRenderService
{
    Task<PdfPageRenderResult> RenderPageAsync(string filePath, int pageNumber, int pixelWidth, CancellationToken cancellationToken);
}

public sealed record PdfPageRenderResult(int PixelWidth, int PixelHeight, int Stride, byte[] Bgra32Pixels);
