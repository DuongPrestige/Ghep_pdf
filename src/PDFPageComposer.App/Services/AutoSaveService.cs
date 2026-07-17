using System.IO;
using PDFPageComposer.App.Interfaces;
using PDFPageComposer.App.Models;

namespace PDFPageComposer.App.Services;

public sealed class AutoSaveService : IAutoSaveService, IDisposable
{
    private readonly IProjectPersistenceService projectPersistenceService;
    private readonly TimeSpan debounceDelay;
    private readonly TimeSpan minimumSaveInterval;
    private readonly object gate = new();
    private readonly Timer timer;
    private ProjectState? pendingState;
    private DateTimeOffset lastSaveAt = DateTimeOffset.MinValue;
    private bool disposed;

    public AutoSaveService(IProjectPersistenceService projectPersistenceService)
        : this(
            projectPersistenceService,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PDFPageComposer", "recovery.ppc.json"),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(10))
    {
    }

    public AutoSaveService(
        IProjectPersistenceService projectPersistenceService,
        string recoveryPath,
        TimeSpan debounceDelay,
        TimeSpan minimumSaveInterval)
    {
        this.projectPersistenceService = projectPersistenceService;
        RecoveryPath = recoveryPath;
        this.debounceDelay = debounceDelay;
        this.minimumSaveInterval = minimumSaveInterval;
        timer = new Timer(OnTimerElapsed);
    }

    public string RecoveryPath { get; }

    public bool HasRecovery => File.Exists(RecoveryPath);

    public void Schedule(ProjectState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        ObjectDisposedException.ThrowIf(disposed, this);

        lock (gate)
        {
            pendingState = state;
            timer.Change(debounceDelay, Timeout.InfiniteTimeSpan);
        }
    }

    public async Task FlushAsync(CancellationToken cancellationToken)
    {
        ProjectState? state;
        lock (gate)
        {
            state = pendingState;
            pendingState = null;
            timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        if (state is null)
        {
            return;
        }

        await SaveAsync(state, cancellationToken);
    }

    public async Task<ProjectState?> LoadRecoveryAsync(CancellationToken cancellationToken)
    {
        if (!HasRecovery)
        {
            return null;
        }

        return await projectPersistenceService.LoadAsync(RecoveryPath, cancellationToken);
    }

    public void ClearRecovery()
    {
        lock (gate)
        {
            pendingState = null;
            timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        if (File.Exists(RecoveryPath))
        {
            File.Delete(RecoveryPath);
        }
    }

    private void OnTimerElapsed(object? state)
    {
        _ = SavePendingIfDueAsync();
    }

    private async Task SavePendingIfDueAsync()
    {
        ProjectState? state;
        lock (gate)
        {
            if (pendingState is null)
            {
                return;
            }

            var dueAt = lastSaveAt + minimumSaveInterval;
            var now = DateTimeOffset.UtcNow;
            if (now < dueAt)
            {
                timer.Change(dueAt - now, Timeout.InfiniteTimeSpan);
                return;
            }

            state = pendingState;
            pendingState = null;
        }

        await SaveAsync(state, CancellationToken.None);
    }

    private async Task SaveAsync(ProjectState state, CancellationToken cancellationToken)
    {
        await projectPersistenceService.SaveAsync(state, RecoveryPath, cancellationToken);
        lastSaveAt = DateTimeOffset.UtcNow;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        timer.Dispose();
    }
}
