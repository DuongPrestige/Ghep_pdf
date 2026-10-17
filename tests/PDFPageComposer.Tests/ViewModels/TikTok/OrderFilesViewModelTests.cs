using System.IO;
using PDFPageComposer.App.Interfaces;
using PDFPageComposer.App.TikTok.Models;
using PDFPageComposer.App.ViewModels.TikTok;
using static PDFPageComposer.Tests.TikTok.Matching.MatchingTestData;

namespace PDFPageComposer.Tests.ViewModels.TikTok;

public sealed class OrderFilesViewModelTests
{
    private static readonly PdfAsset Cody1 = Asset("CODY", 1, 6, 10);
    private static readonly PdfAsset Cody2 = Asset("CODY", 2, 23, 9);
    private static readonly PdfAsset Cody9 = Asset("CODY", 9, 1, 1);
    private static readonly PdfAsset Ivan3 = Asset("IVAN", 3, 15, 10);

    [Fact]
    public void Library_rows_list_the_idol_files_newest_first_and_hide_files_missing_on_disk()
    {
        var gone = Asset("CODY", 2, 28, 8);
        var viewModel = Create(Order("O1", 0, [("CODY20", 1)]), [Cody1, Cody2], [Ivan3, Cody9, Cody2, Cody1, gone], path => path != gone.FilePath);

        Assert.Equal("CODY", viewModel.FilterText);
        Assert.Equal([Cody1, Cody2, Cody9], viewModel.LibraryFiles.Select(row => row.Asset));
        Assert.Equal(9, viewModel.LibraryFiles[2].Pages.Count);
        Assert.Equal(1, viewModel.MissingFileCount);
        Assert.Contains("Quét kho", viewModel.MissingFilesText);

        viewModel.FilterText = "IVAN";
        Assert.Equal([Ivan3], viewModel.LibraryFiles.Select(row => row.Asset));
    }

    [Fact]
    public async Task Clicking_pages_builds_the_set_in_print_order_and_saves_page_runs()
    {
        var renderer = new CountingRenderer();
        var viewModel = Create(Order("O1", 0, [("CODY20", 2)]), [Cody1, Cody2], [Cody9, Cody2, Cody1], _ => true, renderer);

        Assert.Equal(2, viewModel.Slots.Count);
        Assert.True(viewModel.HasManySlots);
        var first = viewModel.Slots[0];
        Assert.Equal("3/3 trang", first.StatusText);
        Assert.Equal([1, 2, 3], first.Pages.Select(page => page.Position));
        var cody2Row = viewModel.LibraryFiles.Single(row => row.Asset == Cody2);
        Assert.Equal([2, 3], cody2Row.Pages.Select(page => page.Position));
        Assert.False(viewModel.CanSave);

        // Second set: pages 7, 8, 9 of the 9-page file, then remove 8 and add page 2 instead.
        viewModel.SelectedSlot = viewModel.Slots[1];
        var cody9Row = viewModel.LibraryFiles.Single(row => row.Asset == Cody9);
        Assert.All(cody2Row.Pages, page => Assert.False(page.IsSelected));
        viewModel.TogglePageCommand.Execute(cody9Row.Pages[6]);
        viewModel.TogglePageCommand.Execute(cody9Row.Pages[7]);
        viewModel.TogglePageCommand.Execute(cody9Row.Pages[8]);
        viewModel.TogglePageCommand.Execute(cody9Row.Pages[7]);
        viewModel.TogglePageCommand.Execute(cody9Row.Pages[1]);
        Assert.Equal([7, 9, 2], viewModel.SelectedPages!.Select(page => page.PageNumber));
        Assert.Equal(3, cody9Row.Pages[1].Position);
        Assert.True(viewModel.CanSave);

        viewModel.MovePageLeftCommand.Execute(viewModel.SelectedPages![2]);
        Assert.Equal([7, 2, 9], viewModel.SelectedPages!.Select(page => page.PageNumber));

        await viewModel.RenderThumbnailCommand.ExecuteAsync(cody9Row.Pages[6]);
        await viewModel.RenderThumbnailCommand.ExecuteAsync(viewModel.SelectedPages![0]);
        Assert.NotNull(viewModel.SelectedPages![0].Image);
        Assert.Equal(1, renderer.Calls);

        viewModel.SaveCommand.Execute(null);

        Assert.Equal(OrderFilesResult.Save, viewModel.Result);
        Assert.Equal(
            [
                new FileOverride("O1", "CODY20", 1, 1, Cody1.FilePath, 1, 1),
                new FileOverride("O1", "CODY20", 1, 2, Cody2.FilePath, 2, 1),
                new FileOverride("O1", "CODY20", 2, 1, Cody9.FilePath, 1, 7),
                new FileOverride("O1", "CODY20", 2, 2, Cody9.FilePath, 1, 2),
                new FileOverride("O1", "CODY20", 2, 3, Cody9.FilePath, 1, 9),
            ],
            viewModel.BuildOverrides());
    }

    [Fact]
    public void Whole_file_clearing_and_going_back_to_automatic()
    {
        var viewModel = Create(Order("O1", 0, [("CODY20", 1)]), [Cody1, Cody2], [Cody1, Cody2, Cody9], _ => true);
        var closed = 0;
        viewModel.CloseRequested += (_, _) => closed++;

        viewModel.ClearSelectedSlotCommand.Execute(null);
        Assert.False(viewModel.CanSave);
        Assert.Contains("chưa chọn trang nào", viewModel.ValidationText);
        viewModel.SaveCommand.Execute(null);
        Assert.Equal(0, closed);

        // Fewer or more pages than the card count are allowed, with a warning.
        viewModel.AddWholeFileCommand.Execute(viewModel.LibraryFiles.Single(row => row.Asset == Cody2));
        Assert.Equal("2/3 trang", viewModel.Slots[0].StatusText);
        Assert.True(viewModel.CanSave);
        Assert.Equal(string.Empty, viewModel.ValidationText);
        Assert.Contains("in ít hơn 1 tờ", viewModel.WarningText);
        viewModel.AddWholeFileCommand.Execute(viewModel.LibraryFiles.Single(row => row.Asset == Cody1));
        viewModel.TogglePageCommand.Execute(viewModel.LibraryFiles.Single(row => row.Asset == Cody9).Pages[0]);
        Assert.Equal("4/3 trang", viewModel.Slots[0].StatusText);
        Assert.True(viewModel.CanSave);
        Assert.Contains("in nhiều hơn 1 tờ", viewModel.WarningText);
        viewModel.TogglePageCommand.Execute(viewModel.LibraryFiles.Single(row => row.Asset == Cody9).Pages[0]);
        viewModel.RemovePageCommand.Execute(viewModel.SelectedPages![2]);

        viewModel.RemovePageCommand.Execute(viewModel.SelectedPages![0]);
        Assert.Equal([2], viewModel.SelectedPages!.Select(page => page.PageNumber));

        viewModel.UseAutomaticCommand.Execute(null);
        Assert.Equal(1, closed);
        Assert.Equal(OrderFilesResult.Automatic, viewModel.Result);
    }

    [Fact]
    public void A_chosen_page_of_a_file_missing_on_disk_blocks_saving()
    {
        var viewModel = Create(Order("O1", 0, [("CODY20", 1)]), [Cody1, Cody2], [Cody1, Cody2], path => path != Cody2.FilePath);

        Assert.True(viewModel.Slots[0].IsComplete);
        Assert.False(viewModel.CanSave);
        Assert.Contains("không còn trên ổ đĩa", viewModel.ValidationText);
    }

    [Fact]
    public async Task Pdfs_added_from_outside_the_library_stay_on_top_and_save_their_page_count()
    {
        const string outside = @"D:\ngoai\ANH KHACH GUI.pdf";
        var picked = new[] { outside, Cody9.FilePath, @"D:\ngoai\hong.pdf" };
        var viewModel = new OrderFilesViewModel(
            Order("O1", 0, [("CODY20", 1)]),
            Skus(Auto("CODY20", "CODY", 20)),
            [Cody1, Cody2, Cody9, Ivan3],
            [],
            [],
            renderer: null,
            fileExists: _ => true,
            pickPdfFiles: () => picked,
            countPdfPages: path => path.EndsWith("hong.pdf", StringComparison.Ordinal) ? throw new IOException("hỏng") : 4);
        viewModel.FilterText = "IVAN";

        Assert.True(viewModel.CanAddExternalFiles);
        await viewModel.AddExternalFilesCommand.ExecuteAsync(null);

        Assert.Equal([outside, Cody9.FilePath, Ivan3.FilePath], viewModel.LibraryFiles.Select(row => row.Asset.FilePath));
        Assert.True(viewModel.LibraryFiles[0].IsExternal);
        Assert.False(viewModel.LibraryFiles[1].IsExternal);
        Assert.Equal(4, viewModel.LibraryFiles[0].Pages.Count);
        Assert.Contains("hong.pdf", viewModel.ExternalStatus);

        viewModel.TogglePageCommand.Execute(viewModel.LibraryFiles[0].Pages[1]);
        viewModel.TogglePageCommand.Execute(viewModel.LibraryFiles[0].Pages[2]);
        viewModel.TogglePageCommand.Execute(viewModel.LibraryFiles[1].Pages[0]);
        Assert.True(viewModel.CanSave);

        Assert.Equal(
            [
                new FileOverride("O1", "CODY20", 1, 1, outside, 2, 2, FilePageCount: 4),
                new FileOverride("O1", "CODY20", 1, 2, Cody9.FilePath, 1, 1),
            ],
            viewModel.BuildOverrides());
    }

    private static OrderFilesViewModel Create(
        TikTokOrder order,
        PdfAsset[] current,
        PdfAsset[] library,
        Func<string, bool> fileExists,
        IPdfRenderService? renderer = null)
    {
        // First set: page 1 of the 1-page file, then both pages of the 2-page file.
        var copies = new[]
        {
            new PrintPlanCopy("O1", "CODY20", 1, 1, 1, "CODY", 1, current[0], DuplicateImage: false),
            new PrintPlanCopy("O1", "CODY20", 1, 1, 1, "CODY", 2, current[1], DuplicateImage: false),
        };
        return new OrderFilesViewModel(
            order,
            Skus(Auto("CODY20", "CODY NAM VO", 20)),
            library,
            [new IdolAlias("CODYNAMVO", "CODY")],
            copies,
            renderer,
            fileExists);
    }

    private sealed class CountingRenderer : IPdfRenderService
    {
        public int Calls { get; private set; }

        public Task<PdfPageRenderResult> RenderPageAsync(string filePath, int pageNumber, int pixelWidth, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new PdfPageRenderResult(1, 1, 4, new byte[4]));
        }
    }
}
