using System.IO;
using PDFPageComposer.App.Models;
using PDFPageComposer.App.Services;

namespace PDFPageComposer.Tests.Services;

public sealed class ProjectPersistenceServiceTests
{
    [Fact]
    public async Task SaveAsync_and_LoadAsync_round_trip_project_state()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"pdf-page-composer-project-{Guid.NewGuid():N}");
        var projectPath = Path.Combine(directory, "project.ppc.json");
        var sourceFile = new SourcePdfFile(Guid.NewGuid(), @"D:\Docs\A.pdf", "A.pdf", 3, 1234, "fingerprint");
        var groupId = Guid.NewGuid();
        var group = new OutputGroup(groupId, "Group 1", DateTimeOffset.UtcNow,
        [
            new OutputPageItem(Guid.NewGuid(), groupId, sourceFile.Id, 2),
            new OutputPageItem(Guid.NewGuid(), groupId, sourceFile.Id, 3)
        ], "#059669");
        var state = ProjectStateMapper.FromSession([sourceFile], [group], thumbnailZoom: 1.25, lastExportPath: @"D:\Out\merged.pdf");
        var service = new ProjectPersistenceService();

        try
        {
            await service.SaveAsync(state, projectPath, CancellationToken.None);

            var loaded = await service.LoadAsync(projectPath, CancellationToken.None);

            Assert.Equal(1, loaded.Version);
            Assert.Single(loaded.SourceFiles);
            Assert.Equal(sourceFile.Id, loaded.SourceFiles[0].Id);
            Assert.Equal(sourceFile.FilePath, loaded.SourceFiles[0].FilePath);
            Assert.Single(loaded.OutputGroups);
            Assert.Equal("#059669", loaded.OutputGroups[0].ColorHex);
            Assert.Equal([2, 3], loaded.OutputGroups[0].Items.Select(item => item.SourcePageNumber));
            Assert.Equal(1.25, loaded.UiState.ThumbnailZoom);
            Assert.Equal(@"D:\Out\merged.pdf", loaded.LastExportPath);
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
    public void ProjectStateMapper_assigns_color_when_loading_older_project_groups()
    {
        var sourceFileId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var state = new ProjectState
        {
            SourceFiles =
            [
                new ProjectSourceFile
                {
                    Id = sourceFileId,
                    FilePath = @"D:\Docs\A.pdf",
                    DisplayName = "A.pdf",
                    PageCount = 1,
                    FileSize = 1234,
                    Fingerprint = "fingerprint"
                }
            ],
            OutputGroups =
            [
                new ProjectOutputGroup
                {
                    Id = groupId,
                    Name = "Group 1",
                    CreatedAt = DateTimeOffset.UtcNow,
                    Items =
                    [
                        new ProjectOutputPageItem
                        {
                            Id = Guid.NewGuid(),
                            SourceFileId = sourceFileId,
                            SourcePageNumber = 1
                        }
                    ]
                }
            ]
        };

        var session = ProjectStateMapper.ToSession(state);

        Assert.Equal("#2563EB", session.OutputGroups.Single().ColorHex);
    }

    [Fact]
    public void ProjectStateMapper_does_not_embed_pdf_content_or_password_fields()
    {
        var sourceFile = new SourcePdfFile(Guid.NewGuid(), @"D:\Docs\A.pdf", "A.pdf", 1, 1234, "fingerprint");

        var state = ProjectStateMapper.FromSession([sourceFile], [], thumbnailZoom: 1.0);
        var serializedPropertyNames = string.Join(
            "|",
            typeof(ProjectState).Assembly
                .GetTypes()
                .Where(type => type.Namespace == typeof(ProjectState).Namespace && type.Name.StartsWith("Project", StringComparison.Ordinal))
                .SelectMany(type => type.GetProperties())
                .Select(property => property.Name));

        Assert.DoesNotContain("Password", serializedPropertyNames, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Content", serializedPropertyNames, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(sourceFile.FilePath, state.SourceFiles.Single().FilePath);
    }

    [Fact]
    public async Task LoadAsync_rejects_unsupported_version()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"pdf-page-composer-project-{Guid.NewGuid():N}");
        var projectPath = Path.Combine(directory, "project.ppc.json");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(projectPath, """{"Version":999}""");
        var service = new ProjectPersistenceService();

        try
        {
            await Assert.ThrowsAsync<NotSupportedException>(() => service.LoadAsync(projectPath, CancellationToken.None));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
