using System.IO;
using PDFPageComposer.App.Models;

namespace PDFPageComposer.App.Services;

public static class ProjectStateMapper
{
    public static ProjectState FromSession(
        IEnumerable<SourcePdfFile> sourceFiles,
        IEnumerable<OutputGroup> outputGroups,
        double thumbnailZoom,
        string? lastExportPath = null)
    {
        return new ProjectState
        {
            SourceFiles = sourceFiles.Select(file => new ProjectSourceFile
            {
                Id = file.Id,
                FilePath = file.FilePath,
                DisplayName = file.DisplayName,
                PageCount = file.PageCount,
                FileSize = file.FileSize,
                Fingerprint = file.Fingerprint,
                IsCollapsed = file.IsCollapsed
            }).ToList(),
            OutputGroups = outputGroups.Select(group => new ProjectOutputGroup
            {
                Id = group.Id,
                Name = group.Name,
                CreatedAt = group.CreatedAt,
                ColorHex = group.ColorHex,
                IsCollapsed = group.IsCollapsed,
                Items = group.Items.Select(item => new ProjectOutputPageItem
                {
                    Id = item.Id,
                    SourceFileId = item.SourceFileId,
                    SourcePageNumber = item.SourcePageNumber
                }).ToList()
            }).ToList(),
            UiState = new ProjectUiState
            {
                ThumbnailZoom = thumbnailZoom
            },
            LastExportPath = lastExportPath
        };
    }

    public static ProjectSessionSnapshot ToSession(ProjectState projectState)
    {
        ArgumentNullException.ThrowIfNull(projectState);

        var sourceFiles = projectState.SourceFiles.Select(file =>
        {
            var pages = Enumerable.Range(1, file.PageCount)
                .Select(pageNumber => new SourcePdfPage(Guid.NewGuid(), file.Id, pageNumber));
            var sourceFile = new SourcePdfFile(
                file.Id,
                file.FilePath,
                file.DisplayName,
                file.PageCount,
                file.FileSize,
                file.Fingerprint,
                pages: pages);
            sourceFile.IsCollapsed = file.IsCollapsed;
            sourceFile.IsMissing = !File.Exists(file.FilePath);
            return sourceFile;
        }).ToList();

        var outputGroups = projectState.OutputGroups.Select((group, index) =>
        {
            var items = group.Items.Select(item => new OutputPageItem(
                item.Id,
                group.Id,
                item.SourceFileId,
                item.SourcePageNumber));
            var outputGroup = new OutputGroup(
                group.Id,
                group.Name,
                group.CreatedAt,
                items,
                string.IsNullOrWhiteSpace(group.ColorHex)
                    ? GetDefaultGroupColor(index)
                    : group.ColorHex);
            outputGroup.IsCollapsed = group.IsCollapsed;
            return outputGroup;
        }).ToList();

        return new ProjectSessionSnapshot(
            sourceFiles,
            outputGroups,
            projectState.UiState.ThumbnailZoom,
            projectState.LastExportPath);
    }

    private static string GetDefaultGroupColor(int index)
    {
        string[] colors =
        [
            "#2563EB",
            "#059669",
            "#D97706",
            "#DC2626",
            "#7C3AED",
            "#0891B2",
            "#C026D3",
            "#65A30D",
            "#EA580C",
            "#4F46E5"
        ];

        return colors[index % colors.Length];
    }
}

public sealed record ProjectSessionSnapshot(
    IReadOnlyList<SourcePdfFile> SourceFiles,
    IReadOnlyList<OutputGroup> OutputGroups,
    double ThumbnailZoom,
    string? LastExportPath);
