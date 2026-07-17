using PDFPageComposer.App.Models;

namespace PDFPageComposer.App.Interfaces;

public interface IAppSettingsService
{
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken);

    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken);
}
