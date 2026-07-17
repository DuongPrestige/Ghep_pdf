using System.IO;
using System.Text.Json;
using PDFPageComposer.App.Interfaces;
using PDFPageComposer.App.Models;

namespace PDFPageComposer.App.Services;

public sealed class ProjectPersistenceService : IProjectPersistenceService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public async Task SaveAsync(ProjectState projectState, string projectPath, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(projectState);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);

        var fullPath = Path.GetFullPath(projectPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = $"{fullPath}.{Guid.NewGuid():N}.tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, projectState, JsonOptions, cancellationToken);
        }

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        File.Move(tempPath, fullPath);
    }

    public async Task<ProjectState> LoadAsync(string projectPath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);

        await using var stream = File.OpenRead(projectPath);
        var state = await JsonSerializer.DeserializeAsync<ProjectState>(stream, JsonOptions, cancellationToken)
            ?? throw new InvalidDataException("Project file is empty or invalid.");

        if (state.Version != 1)
        {
            throw new NotSupportedException($"Unsupported project version {state.Version}.");
        }

        return state;
    }
}
