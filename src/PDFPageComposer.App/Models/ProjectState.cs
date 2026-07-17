namespace PDFPageComposer.App.Models;

public sealed class ProjectState
{
    public int Version { get; set; } = 1;

    public List<ProjectSourceFile> SourceFiles { get; set; } = [];

    public List<ProjectOutputGroup> OutputGroups { get; set; } = [];

    public ProjectUiState UiState { get; set; } = new();

    public string? LastExportPath { get; set; }
}

public sealed class ProjectSourceFile
{
    public Guid Id { get; set; }

    public string FilePath { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public int PageCount { get; set; }

    public long FileSize { get; set; }

    public string Fingerprint { get; set; } = string.Empty;

    public bool IsCollapsed { get; set; }
}

public sealed class ProjectOutputGroup
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public bool IsCollapsed { get; set; }

    public List<ProjectOutputPageItem> Items { get; set; } = [];
}

public sealed class ProjectOutputPageItem
{
    public Guid Id { get; set; }

    public Guid SourceFileId { get; set; }

    public int SourcePageNumber { get; set; }
}

public sealed class ProjectUiState
{
    public double ThumbnailZoom { get; set; } = 1.0;

    public double OutputPanelWidth { get; set; }
}
