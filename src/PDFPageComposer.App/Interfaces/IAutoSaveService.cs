using PDFPageComposer.App.Models;

namespace PDFPageComposer.App.Interfaces;

public interface IAutoSaveService
{
    string RecoveryPath { get; }

    bool HasRecovery { get; }

    void Schedule(ProjectState state);

    Task FlushAsync(CancellationToken cancellationToken);

    Task<ProjectState?> LoadRecoveryAsync(CancellationToken cancellationToken);

    void ClearRecovery();
}
