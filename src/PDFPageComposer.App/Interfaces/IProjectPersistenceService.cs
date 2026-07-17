using PDFPageComposer.App.Models;

namespace PDFPageComposer.App.Interfaces;

public interface IProjectPersistenceService
{
    Task SaveAsync(ProjectState projectState, string projectPath, CancellationToken cancellationToken);

    Task<ProjectState> LoadAsync(string projectPath, CancellationToken cancellationToken);
}
