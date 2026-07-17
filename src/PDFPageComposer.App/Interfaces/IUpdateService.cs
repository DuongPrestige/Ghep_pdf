using PDFPageComposer.App.Models;

namespace PDFPageComposer.App.Interfaces;

public interface IUpdateService
{
    Task<AppUpdateResult> CheckAndInstallUpdateAsync(CancellationToken cancellationToken);
}
