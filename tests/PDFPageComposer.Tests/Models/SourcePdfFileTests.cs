using PDFPageComposer.App.Models;

namespace PDFPageComposer.Tests.Models;

public sealed class SourcePdfFileTests
{
    [Fact]
    public void Constructor_creates_one_page_model_per_source_page()
    {
        var fileId = Guid.NewGuid();

        var sourceFile = new SourcePdfFile(
            fileId,
            @"D:\Docs\A.pdf",
            "A.pdf",
            pageCount: 3,
            fileSize: 1024,
            fingerprint: "abc");

        Assert.Equal(3, sourceFile.Pages.Count);
        Assert.Collection(
            sourceFile.Pages,
            page => Assert.Equal(1, page.PageNumber),
            page => Assert.Equal(2, page.PageNumber),
            page => Assert.Equal(3, page.PageNumber));
        Assert.All(sourceFile.Pages, page => Assert.Equal(fileId, page.SourceFileId));
    }

    [Fact]
    public void Source_page_rejects_zero_page_number()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SourcePdfPage(Guid.NewGuid(), Guid.NewGuid(), 0));
    }
}
