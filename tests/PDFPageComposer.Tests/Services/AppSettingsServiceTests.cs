using System.IO;
using PDFPageComposer.App.Models;
using PDFPageComposer.App.Services;

namespace PDFPageComposer.Tests.Services;

public sealed class AppSettingsServiceTests
{
    [Fact]
    public async Task SaveAsync_and_LoadAsync_round_trip_settings_json()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"pdf-page-composer-settings-{Guid.NewGuid():N}");
        var settingsPath = Path.Combine(directory, "settings.json");
        var service = new AppSettingsService(settingsPath);
        var settings = new AppSettings
        {
            FoxitExecutablePath = @"C:\Foxit\FoxitPDFReader.exe",
            ThumbnailCacheLimit = 42,
            LastOpenDirectory = @"D:\Docs",
            UpdateManifestUrl = "https://example.test/pdf-page-composer/latest.json"
        };

        try
        {
            await service.SaveAsync(settings, CancellationToken.None);

            var loaded = await service.LoadAsync(CancellationToken.None);

            Assert.Equal(1, loaded.Version);
            Assert.Equal(settings.FoxitExecutablePath, loaded.FoxitExecutablePath);
            Assert.Equal(42, loaded.ThumbnailCacheLimit);
            Assert.Equal(settings.LastOpenDirectory, loaded.LastOpenDirectory);
            Assert.Equal(settings.UpdateManifestUrl, loaded.UpdateManifestUrl);
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
    public async Task LoadAsync_returns_defaults_when_file_is_missing()
    {
        var settingsPath = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}", "settings.json");
        var service = new AppSettingsService(settingsPath);

        var settings = await service.LoadAsync(CancellationToken.None);

        Assert.Equal(1, settings.Version);
        Assert.Null(settings.FoxitExecutablePath);
        Assert.Equal(
            "https://raw.githubusercontent.com/DuongPrestige/Ghep_pdf/main/latest.json",
            settings.UpdateManifestUrl);
    }
}
