using PDFPageComposer.App.Models;

namespace PDFPageComposer.App.Interfaces;

public interface IPdfExportService
{
    Task ExportAsync(
        IReadOnlyCollection<SourcePdfFile> sourceFiles,
        IReadOnlyCollection<OutputGroup> groups,
        string outputPath,
        IProgress<int>? progress,
        CancellationToken cancellationToken);
}
