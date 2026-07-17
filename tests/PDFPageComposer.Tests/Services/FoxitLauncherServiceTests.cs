using System.IO;
using PDFPageComposer.App.Interfaces;
using PDFPageComposer.App.Models;
using PDFPageComposer.App.Services;

namespace PDFPageComposer.Tests.Services;

public sealed class FoxitLauncherServiceTests
{
    [Fact]
    public async Task DiscoverFoxitExecutableAsync_prefers_valid_configured_path()
    {
        using var fixture = new FoxitFixture();
        var foxitPath = fixture.CreateExecutable("Configured/FoxitPDFReader.exe");
        var settings = new InMemorySettingsService(new AppSettings { FoxitExecutablePath = foxitPath });
        var service = new FoxitLauncherService(settings, new RecordingProcessLauncher(), [fixture.Root]);

        var discovered = await service.DiscoverFoxitExecutableAsync(CancellationToken.None);

        Assert.Equal(foxitPath, discovered);
    }

    [Fact]
    public async Task DiscoverFoxitExecutableAsync_finds_and_saves_installed_candidate()
    {
        using var fixture = new FoxitFixture();
        var foxitPath = fixture.CreateExecutable("Foxit Software/Foxit PDF Reader/FoxitPDFReader.exe");
        var settings = new InMemorySettingsService(new AppSettings());
        var service = new FoxitLauncherService(settings, new RecordingProcessLauncher(), [fixture.Root]);

        var discovered = await service.DiscoverFoxitExecutableAsync(CancellationToken.None);

        Assert.Equal(foxitPath, discovered);
        Assert.Equal(foxitPath, settings.Current.FoxitExecutablePath);
    }

    [Fact]
    public async Task OpenPdfAsync_uses_foxit_when_available()
    {
        using var fixture = new FoxitFixture();
        var foxitPath = fixture.CreateExecutable("FoxitPDFReader.exe");
        var pdfPath = fixture.CreatePdf("output.pdf");
        var launcher = new RecordingProcessLauncher();
        var settings = new InMemorySettingsService(new AppSettings { FoxitExecutablePath = foxitPath });
        var service = new FoxitLauncherService(settings, launcher, [fixture.Root]);

        await service.OpenPdfAsync(pdfPath, CancellationToken.None);

        Assert.Equal(foxitPath, launcher.ExecutablePath);
        Assert.Equal(pdfPath, launcher.Argument);
        Assert.Null(launcher.FilePath);
    }

    [Fact]
    public async Task OpenPdfAsync_falls_back_to_shell_default_without_foxit()
    {
        using var fixture = new FoxitFixture();
        var pdfPath = fixture.CreatePdf("output.pdf");
        var launcher = new RecordingProcessLauncher();
        var service = new FoxitLauncherService(new InMemorySettingsService(new AppSettings()), launcher, [fixture.Root]);

        await service.OpenPdfAsync(pdfPath, CancellationToken.None);

        Assert.Equal(pdfPath, launcher.FilePath);
        Assert.Null(launcher.ExecutablePath);
    }

    private sealed class FoxitFixture : IDisposable
    {
        public FoxitFixture()
        {
            Root = Path.Combine(Path.GetTempPath(), $"pdf-page-composer-foxit-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public string CreateExecutable(string relativePath)
        {
            var path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "fake exe");
            return path;
        }

        public string CreatePdf(string relativePath)
        {
            var path = Path.Combine(Root, relativePath);
            File.WriteAllText(path, "%PDF fake");
            return path;
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
        public InMemorySettingsService(AppSettings current)
        {
            Current = current;
        }

        public AppSettings Current { get; private set; }

        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(Current);
        }

        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
        {
            Current = settings;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingProcessLauncher : IProcessLauncher
    {
        public string? FilePath { get; private set; }

        public string? ExecutablePath { get; private set; }

        public string? Argument { get; private set; }

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
            ExecutablePath = fileName;
            Argument = string.Join(" ", arguments);
        }
    }
}
