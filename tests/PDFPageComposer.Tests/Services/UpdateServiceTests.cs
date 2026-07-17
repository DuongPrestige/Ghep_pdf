using System.IO;
using System.IO.Compression;
using PDFPageComposer.App.Interfaces;
using PDFPageComposer.App.Models;
using PDFPageComposer.App.Services;

namespace PDFPageComposer.Tests.Services;

public sealed class UpdateServiceTests
{
    [Fact]
    public async Task CheckAndInstallUpdateAsync_reports_not_configured_when_manifest_url_is_missing()
    {
        var service = new UpdateService(new InMemorySettingsService(new AppSettings()), new RecordingProcessLauncher());

        var result = await service.CheckAndInstallUpdateAsync(CancellationToken.None);

        Assert.Equal(AppUpdateStatus.NotConfigured, result.Status);
    }

    [Fact]
    public async Task CheckAndInstallUpdateAsync_reports_up_to_date_for_current_version()
    {
        using var fixture = new UpdateFixture();
        var manifestPath = fixture.WriteManifest("""
            {
              "version": "1.0.0",
              "downloadUrl": "unused.zip"
            }
            """);
        var service = new UpdateService(
            new InMemorySettingsService(new AppSettings { UpdateManifestUrl = manifestPath }),
            new RecordingProcessLauncher());

        var result = await service.CheckAndInstallUpdateAsync(CancellationToken.None);

        Assert.Equal(AppUpdateStatus.UpToDate, result.Status);
    }

    [Fact]
    public async Task CheckAndInstallUpdateAsync_extracts_package_and_starts_installer_script_for_new_version()
    {
        using var fixture = new UpdateFixture();
        var packagePath = fixture.CreatePackage();
        var manifestPath = fixture.WriteManifest($$"""
            {
              "version": "99.0.0",
              "downloadUrl": "{{packagePath.Replace("\\", "\\\\", StringComparison.Ordinal)}}"
            }
            """);
        var launcher = new RecordingProcessLauncher();
        var service = new UpdateService(
            new InMemorySettingsService(new AppSettings { UpdateManifestUrl = manifestPath }),
            launcher);

        var result = await service.CheckAndInstallUpdateAsync(CancellationToken.None);

        Assert.Equal(AppUpdateStatus.UpdateStarted, result.Status);
        Assert.Equal("powershell.exe", launcher.FileName);
        Assert.Contains("-File", launcher.Arguments);
        Assert.NotNull(launcher.Arguments.LastOrDefault(argument => argument.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase)));
    }

    private sealed class UpdateFixture : IDisposable
    {
        public UpdateFixture()
        {
            Root = Path.Combine(Path.GetTempPath(), $"pdf-page-composer-update-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public string WriteManifest(string content)
        {
            var path = Path.Combine(Root, "latest.json");
            File.WriteAllText(path, content);
            return path;
        }

        public string CreatePackage()
        {
            var sourceDirectory = Path.Combine(Root, "package-source");
            Directory.CreateDirectory(sourceDirectory);
            File.WriteAllText(Path.Combine(sourceDirectory, "PDFPageComposer.App.exe"), "fake exe");
            var packagePath = Path.Combine(Root, "update.zip");
            ZipFile.CreateFromDirectory(sourceDirectory, packagePath);
            return packagePath;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    private sealed class InMemorySettingsService : IAppSettingsService
    {
        public InMemorySettingsService(AppSettings settings)
        {
            Settings = settings;
        }

        public AppSettings Settings { get; }

        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(Settings);
        }

        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingProcessLauncher : IProcessLauncher
    {
        public string? FilePath { get; private set; }

        public string? ExecutablePath { get; private set; }

        public string? Argument { get; private set; }

        public string? FileName { get; private set; }

        public IReadOnlyList<string> Arguments { get; private set; } = [];

        public void StartFile(string filePath)
        {
            FilePath = filePath;
        }

        public void StartExecutable(string executablePath, string argument)
        {
            ExecutablePath = executablePath;
            Argument = argument;
        }

        public void StartProcess(string fileName, IEnumerable<string> arguments)
        {
            FileName = fileName;
            Arguments = arguments.ToList();
        }
    }
}
