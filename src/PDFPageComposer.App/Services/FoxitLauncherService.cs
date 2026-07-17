using System.IO;
using PDFPageComposer.App.Interfaces;

namespace PDFPageComposer.App.Services;

public sealed class FoxitLauncherService : IFoxitLauncherService
{
    private readonly IAppSettingsService settingsService;
    private readonly IProcessLauncher processLauncher;
    private readonly IReadOnlyList<string> searchRoots;

    public FoxitLauncherService(IAppSettingsService settingsService, IProcessLauncher processLauncher)
        : this(settingsService, processLauncher, GetDefaultSearchRoots())
    {
    }

    public FoxitLauncherService(
        IAppSettingsService settingsService,
        IProcessLauncher processLauncher,
        IReadOnlyList<string> searchRoots)
    {
        this.settingsService = settingsService;
        this.processLauncher = processLauncher;
        this.searchRoots = searchRoots;
    }

    public async Task<string?> DiscoverFoxitExecutableAsync(CancellationToken cancellationToken)
    {
        var settings = await settingsService.LoadAsync(cancellationToken);
        if (IsValidExecutable(settings.FoxitExecutablePath))
        {
            return settings.FoxitExecutablePath;
        }

        foreach (var root in searchRoots.Where(Directory.Exists))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var candidates = EnumerateFoxitCandidates(root);
            var candidate = candidates.FirstOrDefault(IsValidExecutable);
            if (candidate is not null)
            {
                settings.FoxitExecutablePath = candidate;
                await settingsService.SaveAsync(settings, cancellationToken);
                return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateFoxitCandidates(string root)
    {
        foreach (var pattern in new[] { "FoxitPDFReader.exe", "FoxitReader.exe" })
        {
            foreach (var candidate in EnumerateFilesSafely(root, pattern))
            {
                yield return candidate;
            }
        }
    }

    private static IEnumerable<string> EnumerateFilesSafely(string root, string pattern)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var directory = pending.Pop();

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly).ToArray();
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                continue;
            }

            foreach (var file in files)
            {
                yield return file;
            }

            IEnumerable<string> children;
            try
            {
                children = Directory.EnumerateDirectories(directory).ToArray();
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                continue;
            }

            foreach (var child in children)
            {
                pending.Push(child);
            }
        }
    }

    public bool IsValidExecutable(string? executablePath)
    {
        return !string.IsNullOrWhiteSpace(executablePath)
            && string.Equals(Path.GetExtension(executablePath), ".exe", StringComparison.OrdinalIgnoreCase)
            && File.Exists(executablePath);
    }

    public async Task OpenPdfAsync(string pdfPath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pdfPath);
        var fullPath = Path.GetFullPath(pdfPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("PDF file does not exist.", fullPath);
        }

        var foxitPath = await DiscoverFoxitExecutableAsync(cancellationToken);
        if (foxitPath is not null)
        {
            processLauncher.StartExecutable(foxitPath, fullPath);
            return;
        }

        processLauncher.StartFile(fullPath);
    }

    private static IReadOnlyList<string> GetDefaultSearchRoots()
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        };

        return roots.Where(root => !string.IsNullOrWhiteSpace(root)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
