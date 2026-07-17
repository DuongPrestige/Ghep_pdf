namespace PDFPageComposer.App.Models;

public sealed record RenderedPageImage(int PixelWidth, int PixelHeight, int Stride, byte[] Bgra32Pixels);
