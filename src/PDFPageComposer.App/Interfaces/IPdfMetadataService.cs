using PDFPageComposer.App.Models;

namespace PDFPageComposer.App.Interfaces;

public interface IPdfMetadataService
{
    Task<SourcePdfFile> ReadAsync(string filePath, CancellationToken cancellationToken);
}
