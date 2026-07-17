using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PDFPageComposer.App.Interfaces;
using PDFPageComposer.App.Models;

namespace PDFPageComposer.App.Services;

public sealed class UpdateService : IUpdateService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IAppSettingsService settingsService;
    private readonly IProcessLauncher processLauncher;
    private readonly HttpClient httpClient;

    public UpdateService(IAppSettingsService settingsService, IProcessLauncher processLauncher)
        : this(settingsService, processLauncher, new HttpClient())
    {
    }

    public UpdateService(IAppSettingsService settingsService, IProcessLauncher processLauncher, HttpClient httpClient)
    {
        this.settingsService = settingsService;
        this.processLauncher = processLauncher;
        this.httpClient = httpClient;
    }

    public async Task<AppUpdateResult> CheckAndInstallUpdateAsync(CancellationToken cancellationToken)
    {
        var settings = await settingsService.LoadAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(settings.UpdateManifestUrl))
        {
            return new AppUpdateResult(
                AppUpdateStatus.NotConfigured,
                "Chua cau hinh UpdateManifestUrl trong settings.json");
        }

        try
        {
            var manifest = await ReadManifestAsync(settings.UpdateManifestUrl, cancellationToken);
            if (!TryParseVersion(manifest.Version, out var latestVersion))
            {
                return new AppUpdateResult(AppUpdateStatus.Failed, "Manifest update khong hop le", manifest);
            }

            var currentVersion = GetCurrentVersion();
            if (latestVersion <= currentVersion)
            {
                return new AppUpdateResult(
                    AppUpdateStatus.UpToDate,
                    $"Dang dung phien ban moi nhat ({currentVersion})",
                    manifest);
            }

            var packagePath = await DownloadPackageAsync(manifest, cancellationToken);
            await VerifyPackageAsync(packagePath, manifest.Sha256, cancellationToken);
            var stagingDirectory = ExtractPackage(packagePath);
            var scriptPath = CreateInstallScript(stagingDirectory);
            processLauncher.StartProcess(
                "powershell.exe",
                [
                    "-NoProfile",
                    "-ExecutionPolicy",
                    "Bypass",
                    "-File",
                    scriptPath
                ]);

            return new AppUpdateResult(
                AppUpdateStatus.UpdateStarted,
                $"Da tai phien ban {manifest.Version}. Ung dung se khoi dong lai de cap nhat.",
                manifest);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or HttpRequestException or JsonException or InvalidDataException or CryptographicException or InvalidOperationException)
        {
            return new AppUpdateResult(AppUpdateStatus.Failed, $"Cap nhat that bai: {ex.Message}");
        }
    }

    private async Task<AppUpdateManifest> ReadManifestAsync(string manifestUrl, CancellationToken cancellationToken)
    {
        await using var stream = await OpenReadAsync(manifestUrl, cancellationToken);
        var manifest = await JsonSerializer.DeserializeAsync<AppUpdateManifest>(stream, JsonOptions, cancellationToken);
        if (manifest is null || string.IsNullOrWhiteSpace(manifest.Version) || string.IsNullOrWhiteSpace(manifest.DownloadUrl))
        {
            throw new InvalidDataException("Manifest update thieu Version hoac DownloadUrl.");
        }

        return manifest;
    }

    private async Task<string> DownloadPackageAsync(AppUpdateManifest manifest, CancellationToken cancellationToken)
    {
        var updateRoot = Path.Combine(Path.GetTempPath(), "PDFPageComposer", "updates", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(updateRoot);
        var packagePath = Path.Combine(updateRoot, "update.zip");

        await using var input = await OpenReadAsync(manifest.DownloadUrl, cancellationToken);
        await using var output = File.Create(packagePath);
        await input.CopyToAsync(output, cancellationToken);
        return packagePath;
    }

    private static async Task VerifyPackageAsync(string packagePath, string? expectedSha256, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(expectedSha256))
        {
            return;
        }

        await using var stream = File.OpenRead(packagePath);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        var actual = Convert.ToHexString(hash);
        if (!string.Equals(actual, expectedSha256.Replace(" ", string.Empty, StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase))
        {
            throw new CryptographicException("SHA-256 cua goi update khong khop.");
        }
    }

    private static string ExtractPackage(string packagePath)
    {
        var stagingDirectory = Path.Combine(Path.GetDirectoryName(packagePath)!, "staging");
        Directory.CreateDirectory(stagingDirectory);
        ZipFile.ExtractToDirectory(packagePath, stagingDirectory, overwriteFiles: true);
        return stagingDirectory;
    }

    private static string CreateInstallScript(string stagingDirectory)
    {
        var processId = Environment.ProcessId;
        var executablePath = Environment.ProcessPath
            ?? Process.GetCurrentProcess().MainModule?.FileName
            ?? throw new InvalidOperationException("Khong xac dinh duoc file exe hien tai.");
        var installDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var scriptPath = Path.Combine(Path.GetTempPath(), "PDFPageComposer", "updates", $"install-{Guid.NewGuid():N}.ps1");
        Directory.CreateDirectory(Path.GetDirectoryName(scriptPath)!);

        var script = $$"""
$ErrorActionPreference = 'Stop'
$processId = {{processId.ToString(CultureInfo.InvariantCulture)}}
$stagingDirectory = '{{EscapePowerShellLiteral(stagingDirectory)}}'
$installDirectory = '{{EscapePowerShellLiteral(installDirectory)}}'
$executablePath = '{{EscapePowerShellLiteral(executablePath)}}'
Wait-Process -Id $processId -ErrorAction SilentlyContinue
Copy-Item -Path (Join-Path $stagingDirectory '*') -Destination $installDirectory -Recurse -Force
Start-Process -FilePath $executablePath
Remove-Item -LiteralPath $stagingDirectory -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $MyInvocation.MyCommand.Path -Force -ErrorAction SilentlyContinue
""";

        File.WriteAllText(scriptPath, script, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return scriptPath;
    }

    private async Task<Stream> OpenReadAsync(string urlOrPath, CancellationToken cancellationToken)
    {
        if (Uri.TryCreate(urlOrPath, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return await httpClient.GetStreamAsync(uri, cancellationToken);
        }

        var path = Uri.TryCreate(urlOrPath, UriKind.Absolute, out uri) && uri.Scheme == Uri.UriSchemeFile
            ? uri.LocalPath
            : urlOrPath;
        return File.OpenRead(path);
    }

    private static Version GetCurrentVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        return assembly.GetName().Version ?? new Version(1, 0, 0);
    }

    private static bool TryParseVersion(string value, out Version version)
    {
        var normalized = value.Trim().TrimStart('v', 'V');
        return Version.TryParse(normalized, out version!);
    }

    private static string EscapePowerShellLiteral(string value)
    {
        return value.Replace("'", "''", StringComparison.Ordinal);
    }
}
