using System.IO;
using PDFPageComposer.App.Models;
using PDFPageComposer.App.Services;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace PDFPageComposer.Tests.Services;

public sealed class PdfExportServiceTests
{
    [Fact]
    public async Task ExportAsync_copies_pages_in_flattened_output_order_with_duplicates()
    {
        using var fixture = await ExportFixture.CreateAsync();
        var sourceA = fixture.SourceFiles[0];
        var sourceB = fixture.SourceFiles[1];
        var group1Id = Guid.NewGuid();
        var group2Id = Guid.NewGuid();
        var groups = new[]
        {
            new OutputGroup(group1Id, "Group 1", DateTimeOffset.UtcNow,
            [
                new OutputPageItem(Guid.NewGuid(), group1Id, sourceA.Id, 2),
                new OutputPageItem(Guid.NewGuid(), group1Id, sourceB.Id, 1)
            ]),
            new OutputGroup(group2Id, "Group 2", DateTimeOffset.UtcNow,
            [
                new OutputPageItem(Guid.NewGuid(), group2Id, sourceB.Id, 1),
                new OutputPageItem(Guid.NewGuid(), group2Id, sourceA.Id, 1)
            ])
        };
        var progress = new RecordingProgress();

        await fixture.ExportService.ExportAsync(fixture.SourceFiles, groups, fixture.OutputPath, progress, CancellationToken.None);

        using var output = PdfReader.Open(fixture.OutputPath, PdfDocumentOpenMode.Import);
        Assert.Equal(4, output.PageCount);
        Assert.Equal([1, 2, 3, 4], progress.Values);
        Assert.Equal(sourceA.Pages[1].Width, output.Pages[0].Width.Point, precision: 1);
        Assert.Equal(sourceB.Pages[0].Width, output.Pages[1].Width.Point, precision: 1);
        Assert.True(File.Exists(sourceA.FilePath));
        Assert.True(File.Exists(sourceB.FilePath));
    }

    [Fact]
    public async Task ExportAsync_rejects_output_path_that_matches_source()
    {
        using var fixture = await ExportFixture.CreateAsync();
        var source = fixture.SourceFiles[0];
        var groupId = Guid.NewGuid();
        var groups = new[]
        {
            new OutputGroup(groupId, "Group 1", DateTimeOffset.UtcNow,
            [
                new OutputPageItem(Guid.NewGuid(), groupId, source.Id, 1)
            ])
        };

        var exception = await Assert.ThrowsAsync<PdfExportException>(
            () => fixture.ExportService.ExportAsync(fixture.SourceFiles, groups, source.FilePath, null, CancellationToken.None));

        Assert.Equal(PdfExportError.OutputMatchesSource, exception.Error);
    }

    [Fact]
    public async Task ExportAsync_rejects_empty_output()
    {
        using var fixture = await ExportFixture.CreateAsync();

        var exception = await Assert.ThrowsAsync<PdfExportException>(
            () => fixture.ExportService.ExportAsync(fixture.SourceFiles, [], fixture.OutputPath, null, CancellationToken.None));

        Assert.Equal(PdfExportError.EmptyOutput, exception.Error);
    }

    [Fact]
    public async Task ExportAsync_does_not_leave_temp_file_when_validation_fails()
    {
        using var fixture = await ExportFixture.CreateAsync();
        var source = fixture.SourceFiles[0];
        var groupId = Guid.NewGuid();
        var groups = new[]
        {
            new OutputGroup(groupId, "Group 1", DateTimeOffset.UtcNow,
            [
                new OutputPageItem(Guid.NewGuid(), groupId, source.Id, source.PageCount + 1)
            ])
        };

        await Assert.ThrowsAsync<PdfExportException>(
            () => fixture.ExportService.ExportAsync(fixture.SourceFiles, groups, fixture.OutputPath, null, CancellationToken.None));

        Assert.Empty(Directory.GetFiles(fixture.DirectoryPath, "*.tmp"));
        Assert.False(File.Exists(fixture.OutputPath));
    }

    [Fact]
    public async Task ExportAsync_cleans_temp_file_when_canceled_between_pages()
    {
        using var fixture = await ExportFixture.CreateAsync();
        var source = fixture.SourceFiles[0];
        var groupId = Guid.NewGuid();
        var groups = new[]
        {
            new OutputGroup(groupId, "Group 1", DateTimeOffset.UtcNow,
            [
                new OutputPageItem(Guid.NewGuid(), groupId, source.Id, 1),
                new OutputPageItem(Guid.NewGuid(), groupId, source.Id, 2)
            ])
        };
        using var cancellation = new CancellationTokenSource();
        var progress = new CancelAfterFirstProgress(cancellation);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => fixture.ExportService.ExportAsync(fixture.SourceFiles, groups, fixture.OutputPath, progress, cancellation.Token));

        Assert.Empty(Directory.GetFiles(fixture.DirectoryPath, "*.tmp"));
        Assert.False(File.Exists(fixture.OutputPath));
    }

    [Fact]
    public async Task ExportAsync_reports_destination_unavailable_for_invalid_output_directory()
    {
        using var fixture = await ExportFixture.CreateAsync();
        var source = fixture.SourceFiles[0];
        var groupId = Guid.NewGuid();
        var groups = new[]
        {
            new OutputGroup(groupId, "Group 1", DateTimeOffset.UtcNow,
            [
                new OutputPageItem(Guid.NewGuid(), groupId, source.Id, 1)
            ])
        };
        var outputPathInsideFile = Path.Combine(source.FilePath, "merged.pdf");

        var exception = await Assert.ThrowsAsync<PdfExportException>(
            () => fixture.ExportService.ExportAsync(fixture.SourceFiles, groups, outputPathInsideFile, null, CancellationToken.None));

        Assert.Equal(PdfExportError.DestinationUnavailable, exception.Error);
        Assert.IsAssignableFrom<IOException>(exception.InnerException);
    }


    [Fact]
    public async Task ExportAsync_creates_required_ten_page_example_order()
    {
        using var fixture = await ExportFixture.CreateTenPageExampleAsync();
        var sourceA = fixture.SourceFiles[0];
        var sourceB = fixture.SourceFiles[1];
        var group1Id = Guid.NewGuid();
        var group2Id = Guid.NewGuid();
        var group3Id = Guid.NewGuid();
        var group4Id = Guid.NewGuid();
        var groups = new[]
        {
            new OutputGroup(group1Id, "Group 1", DateTimeOffset.UtcNow,
            [
                new OutputPageItem(Guid.NewGuid(), group1Id, sourceA.Id, 3),
                new OutputPageItem(Guid.NewGuid(), group1Id, sourceA.Id, 4)
            ]),
            new OutputGroup(group2Id, "Group 2", DateTimeOffset.UtcNow,
            [
                new OutputPageItem(Guid.NewGuid(), group2Id, sourceB.Id, 1),
                new OutputPageItem(Guid.NewGuid(), group2Id, sourceB.Id, 2),
                new OutputPageItem(Guid.NewGuid(), group2Id, sourceB.Id, 3)
            ]),
            new OutputGroup(group3Id, "Group 3", DateTimeOffset.UtcNow,
            [
                new OutputPageItem(Guid.NewGuid(), group3Id, sourceB.Id, 4),
                new OutputPageItem(Guid.NewGuid(), group3Id, sourceB.Id, 5)
            ]),
            new OutputGroup(group4Id, "Group 4", DateTimeOffset.UtcNow,
            [
                new OutputPageItem(Guid.NewGuid(), group4Id, sourceB.Id, 1),
                new OutputPageItem(Guid.NewGuid(), group4Id, sourceB.Id, 2),
                new OutputPageItem(Guid.NewGuid(), group4Id, sourceB.Id, 3)
            ])
        };

        await fixture.ExportService.ExportAsync(fixture.SourceFiles, groups, fixture.OutputPath, null, CancellationToken.None);

        using var output = PdfReader.Open(fixture.OutputPath, PdfDocumentOpenMode.Import);
        Assert.Equal(10, output.PageCount);
        Assert.Equal(
            [303, 304, 401, 402, 403, 404, 405, 401, 402, 403],
            output.Pages.Cast<PdfPage>().Select(page => (int)Math.Round(page.Width.Point)));
        Assert.True(File.Exists(sourceA.FilePath));
        Assert.True(File.Exists(sourceB.FilePath));
    }

    private sealed class ExportFixture : IDisposable
    {
        private ExportFixture(string directoryPath, IReadOnlyList<SourcePdfFile> sourceFiles)
        {
            DirectoryPath = directoryPath;
            SourceFiles = sourceFiles;
        }

        public string DirectoryPath { get; }

        public string OutputPath => Path.Combine(DirectoryPath, "output.pdf");

        public IReadOnlyList<SourcePdfFile> SourceFiles { get; }

        public PdfExportService ExportService { get; } = new();

        public static async Task<ExportFixture> CreateAsync()
        {
            var directory = Path.Combine(Path.GetTempPath(), $"pdf-page-composer-export-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            var sourceAPath = Path.Combine(directory, "A.pdf");
            var sourceBPath = Path.Combine(directory, "B.pdf");
            CreatePdf(sourceAPath, [new PageSize(300, 500), new PageSize(420, 600)]);
            CreatePdf(sourceBPath, [new PageSize(612, 792)]);

            using var pdfium = new PdfiumLibrary();
            var metadata = new PdfMetadataService(pdfium);
            var sourceFiles = new[]
            {
                await metadata.ReadAsync(sourceAPath, CancellationToken.None),
                await metadata.ReadAsync(sourceBPath, CancellationToken.None)
            };

            return new ExportFixture(directory, sourceFiles);
        }

        public static async Task<ExportFixture> CreateTenPageExampleAsync()
        {
            var directory = Path.Combine(Path.GetTempPath(), $"pdf-page-composer-example-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            var sourceAPath = Path.Combine(directory, "A.pdf");
            var sourceBPath = Path.Combine(directory, "B.pdf");
            CreatePdf(sourceAPath, [new PageSize(301, 500), new PageSize(302, 500), new PageSize(303, 500), new PageSize(304, 500)]);
            CreatePdf(sourceBPath, [new PageSize(401, 500), new PageSize(402, 500), new PageSize(403, 500), new PageSize(404, 500), new PageSize(405, 500)]);

            using var pdfium = new PdfiumLibrary();
            var metadata = new PdfMetadataService(pdfium);
            var sourceFiles = new[]
            {
                await metadata.ReadAsync(sourceAPath, CancellationToken.None),
                await metadata.ReadAsync(sourceBPath, CancellationToken.None)
            };

            return new ExportFixture(directory, sourceFiles);
        }

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, recursive: true);
            }
        }

        private static void CreatePdf(string path, IReadOnlyList<PageSize> pageSizes)
        {
            using var document = new PdfDocument();
            foreach (var pageSize in pageSizes)
            {
                var page = document.AddPage();
                page.Width = XUnit.FromPoint(pageSize.Width);
                page.Height = XUnit.FromPoint(pageSize.Height);
            }

            document.Save(path);
        }
    }

    private sealed record PageSize(double Width, double Height);

    private sealed class CancelAfterFirstProgress : IProgress<int>
    {
        private readonly CancellationTokenSource cancellation;

        public CancelAfterFirstProgress(CancellationTokenSource cancellation)
        {
            this.cancellation = cancellation;
        }

        public void Report(int value)
        {
            if (value == 1)
            {
                cancellation.Cancel();
            }
        }
    }

    private sealed class RecordingProgress : IProgress<int>
    {
        public List<int> Values { get; } = [];

        public void Report(int value)
        {
            Values.Add(value);
        }
    }
}
