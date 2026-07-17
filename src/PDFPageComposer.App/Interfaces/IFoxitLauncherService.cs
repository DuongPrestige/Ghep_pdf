namespace PDFPageComposer.App.Interfaces;

public interface IFoxitLauncherService
{
    Task<string?> DiscoverFoxitExecutableAsync(CancellationToken cancellationToken);

    bool IsValidExecutable(string? executablePath);

    Task OpenPdfAsync(string pdfPath, CancellationToken cancellationToken);
}
