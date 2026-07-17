using System.IO;
using PDFPageComposer.App.Models;
using PDFPageComposer.App.Services;

namespace PDFPageComposer.Tests.Services;

public sealed class AutoSaveServiceTests
{
    [Fact]
    public async Task Schedule_defers_write_until_flush_and_persists_latest_state()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"pdf-page-composer-autosave-{Guid.NewGuid():N}");
        var recoveryPath = Path.Combine(directory, "recovery.ppc.json");
        var persistence = new ProjectPersistenceService();
        using var service = new AutoSaveService(
            persistence,
            recoveryPath,
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(1));

        try
        {
            service.Schedule(new ProjectState { UiState = new ProjectUiState { ThumbnailZoom = 0.75 } });
            service.Schedule(new ProjectState { UiState = new ProjectUiState { ThumbnailZoom = 1.25 } });

            Assert.False(File.Exists(recoveryPath));

            await service.FlushAsync(CancellationToken.None);

            Assert.True(service.HasRecovery);
            var recovered = await service.LoadRecoveryAsync(CancellationToken.None);
            Assert.NotNull(recovered);
            Assert.Equal(1.25, recovered.UiState.ThumbnailZoom);
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ClearRecovery_removes_recovery_file_and_pending_state()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"pdf-page-composer-autosave-{Guid.NewGuid():N}");
        var recoveryPath = Path.Combine(directory, "recovery.ppc.json");
        var persistence = new ProjectPersistenceService();
        using var service = new AutoSaveService(
            persistence,
            recoveryPath,
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(1));

        try
        {
            service.Schedule(new ProjectState());
            await service.FlushAsync(CancellationToken.None);

            service.Schedule(new ProjectState { UiState = new ProjectUiState { ThumbnailZoom = 1.5 } });
            service.ClearRecovery();
            await service.FlushAsync(CancellationToken.None);

            Assert.False(service.HasRecovery);
            Assert.False(File.Exists(recoveryPath));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
