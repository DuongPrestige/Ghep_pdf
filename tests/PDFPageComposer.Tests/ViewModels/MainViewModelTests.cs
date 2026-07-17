using System.IO;
using System.Diagnostics;
using PDFPageComposer.App.Interfaces;
using PDFPageComposer.App.Models;
using PDFPageComposer.App.Services;
using PDFPageComposer.App.ViewModels;

namespace PDFPageComposer.Tests.ViewModels;

public sealed class MainViewModelTests
{
    [Fact]
    public async Task ImportPdfFilesAsync_imports_only_pdf_paths()
    {
        var metadata = new FakePdfMetadataService();
        metadata.Add(@"D:\Docs\a.pdf", CreateSourceFile(@"D:\Docs\a.pdf", 2));
        var viewModel = CreateViewModelWithServices(metadata: metadata);

        await viewModel.ImportPdfFilesAsync([@"D:\Docs\a.pdf", @"D:\Docs\a.txt"], CancellationToken.None);

        Assert.Single(viewModel.SourceFiles);
        Assert.Equal(2, viewModel.SourcePageCount);
        Assert.Equal("Đã import 1/1 file PDF", viewModel.RenderStatus);
    }

    [Fact]
    public async Task ImportPdfFilesAsync_keeps_importing_after_file_error()
    {
        var metadata = new FakePdfMetadataService();
        metadata.Add(@"D:\Docs\a.pdf", CreateSourceFile(@"D:\Docs\a.pdf", 1));
        metadata.AddError(@"D:\Docs\b.pdf", PdfMetadataError.InvalidPdf);
        var viewModel = CreateViewModelWithServices(metadata: metadata);

        await viewModel.ImportPdfFilesAsync([@"D:\Docs\b.pdf", @"D:\Docs\a.pdf"], CancellationToken.None);

        Assert.Single(viewModel.SourceFiles);
        Assert.Equal("Đã import 1/2 file PDF", viewModel.RenderStatus);
        Assert.Single(viewModel.ImportErrors);
        Assert.Equal("b.pdf: PDF lỗi hoặc hỏng", viewModel.ImportErrors[0]);
    }

    [Fact]
    public async Task ImportPdfFilesAsync_records_password_required_error_without_stopping_batch()
    {
        var metadata = new FakePdfMetadataService();
        metadata.AddError(@"D:\Docs\locked.pdf", PdfMetadataError.PasswordRequired);
        metadata.Add(@"D:\Docs\a.pdf", CreateSourceFile(@"D:\Docs\a.pdf", 1));
        var viewModel = CreateViewModelWithServices(metadata: metadata);

        await viewModel.ImportPdfFilesAsync([@"D:\Docs\locked.pdf", @"D:\Docs\a.pdf"], CancellationToken.None);

        Assert.Single(viewModel.SourceFiles);
        Assert.Equal("locked.pdf: cần mật khẩu", viewModel.ImportErrors.Single());
    }

    [Fact]
    public void RemoveSourceFile_removes_file_without_output_items()
    {
        var sourceFile = CreateSourceFile(@"D:\Docs\a.pdf", 1);
        var viewModel = CreateViewModelWithServices();
        viewModel.SourceFiles.Add(sourceFile);

        viewModel.RemoveSourceFileCommand.Execute(sourceFile);

        Assert.Empty(viewModel.SourceFiles);
        Assert.Equal("Đã xóa a.pdf khỏi phiên", viewModel.RenderStatus);
    }

    [Fact]
    public void RemoveSourceFile_keeps_file_when_output_references_it()
    {
        var sourceFile = CreateSourceFile(@"D:\Docs\a.pdf", 1);
        var groupId = Guid.NewGuid();
        var viewModel = CreateViewModelWithServices();
        viewModel.SourceFiles.Add(sourceFile);
        viewModel.OutputGroups.Add(new OutputGroup(
            groupId,
            "Group 1",
            DateTimeOffset.UtcNow,
            [new OutputPageItem(Guid.NewGuid(), groupId, sourceFile.Id, 1)]));

        viewModel.RemoveSourceFileCommand.Execute(sourceFile);

        Assert.Single(viewModel.SourceFiles);
        Assert.Equal("a.pdf: đang có trang trong đầu ra", viewModel.RenderStatus);
    }

    [Fact]
    public async Task RenderThumbnailCommand_renders_page_that_has_no_thumbnail()
    {
        var sourceFile = CreateSourceFile(@"D:\Docs\a.pdf", 1);
        var render = new FakePdfRenderService();
        var viewModel = CreateViewModelWithServices(render: render);
        viewModel.SourceFiles.Add(sourceFile);

        await viewModel.RenderThumbnailCommand.ExecuteAsync(sourceFile.Pages[0]);

        Assert.Equal(ThumbnailState.Ready, sourceFile.Pages[0].ThumbnailState);
        Assert.True(sourceFile.Pages[0].HasThumbnail);
        Assert.Equal(1, render.CallCount);
    }

    [Fact]
    public async Task RenderThumbnailCommand_allows_multiple_pages_to_queue_at_once()
    {
        var sourceFile = CreateSourceFile(@"D:\Docs\a.pdf", 3);
        var render = new FakePdfRenderService { Delay = TimeSpan.FromMilliseconds(50) };
        var viewModel = CreateViewModelWithServices(render: render);
        viewModel.SourceFiles.Add(sourceFile);

        await Task.WhenAll(sourceFile.Pages.Select(page => viewModel.RenderThumbnailCommand.ExecuteAsync(page)));

        Assert.All(sourceFile.Pages, page => Assert.Equal(ThumbnailState.Ready, page.ThumbnailState));
        Assert.Equal(3, render.CallCount);
    }


    [Fact]
    public async Task RenderThumbnailCommand_does_not_render_ready_page_again()
    {
        var sourceFile = CreateSourceFile(@"D:\Docs\a.pdf", 1);
        var render = new FakePdfRenderService();
        var viewModel = CreateViewModelWithServices(render: render);
        var page = sourceFile.Pages[0];
        page.SetThumbnail(10, 10, 40, new byte[400]);
        viewModel.SourceFiles.Add(sourceFile);

        await viewModel.RenderThumbnailCommand.ExecuteAsync(page);

        Assert.Equal(0, render.CallCount);
    }

    [Fact]
    public async Task RenderThumbnailCommand_marks_page_error_when_render_fails()
    {
        var sourceFile = CreateSourceFile(@"D:\Docs\a.pdf", 1);
        var render = new FakePdfRenderService { Exception = new InvalidOperationException("render failed") };
        var viewModel = CreateViewModelWithServices(render: render);
        viewModel.SourceFiles.Add(sourceFile);

        await viewModel.RenderThumbnailCommand.ExecuteAsync(sourceFile.Pages[0]);

        Assert.Equal(ThumbnailState.Error, sourceFile.Pages[0].ThumbnailState);
        Assert.Equal("render failed", sourceFile.Pages[0].ThumbnailError);
    }

    [Fact]
    public async Task OpenPreviewCommand_renders_selected_page_without_changing_selection()
    {
        var sourceFile = CreateSourceFile(@"D:\Docs\a.pdf", 2);
        var render = new FakePdfRenderService();
        var viewModel = CreateViewModelWithServices(render: render);
        viewModel.SourceFiles.Add(sourceFile);

        await viewModel.OpenPreviewCommand.ExecuteAsync(sourceFile.Pages[1]);

        Assert.True(viewModel.IsPreviewOpen);
        Assert.Same(sourceFile.Pages[1], viewModel.PreviewPage);
        Assert.NotNull(viewModel.PreviewImage);
        Assert.Equal(ThumbnailState.Ready, viewModel.PreviewState);
        Assert.False(sourceFile.Pages[1].IsSelected);
        Assert.Contains("trang 2", viewModel.PreviewTitle);
    }

    [Fact]
    public async Task ClosePreviewCommand_releases_preview_image_and_keeps_selection_state()
    {
        var sourceFile = CreateSourceFile(@"D:\Docs\a.pdf", 1);
        sourceFile.Pages[0].IsSelected = true;
        var viewModel = CreateViewModelWith(sourceFile);

        await viewModel.OpenPreviewCommand.ExecuteAsync(sourceFile.Pages[0]);
        viewModel.ClosePreviewCommand.Execute(null);

        Assert.False(viewModel.IsPreviewOpen);
        Assert.Null(viewModel.PreviewPage);
        Assert.Null(viewModel.PreviewImage);
        Assert.True(sourceFile.Pages[0].IsSelected);
    }

    [Fact]
    public async Task PreviewNavigation_stays_within_file_bounds()
    {
        var sourceFile = CreateSourceFile(@"D:\Docs\a.pdf", 2);
        var viewModel = CreateViewModelWith(sourceFile);

        await viewModel.OpenPreviewCommand.ExecuteAsync(sourceFile.Pages[0]);
        Assert.False(viewModel.CanNavigatePreviewPrevious);
        Assert.True(viewModel.CanNavigatePreviewNext);

        await viewModel.PreviewPreviousCommand.ExecuteAsync(null);
        Assert.Same(sourceFile.Pages[0], viewModel.PreviewPage);

        await viewModel.PreviewNextCommand.ExecuteAsync(null);
        Assert.Same(sourceFile.Pages[1], viewModel.PreviewPage);
        Assert.True(viewModel.CanNavigatePreviewPrevious);
        Assert.False(viewModel.CanNavigatePreviewNext);
    }

    [Fact]
    public async Task PreviewZoomAndFit_render_with_expected_sizes()
    {
        var sourceFile = CreateSourceFile(@"D:\Docs\a.pdf", 1);
        var render = new FakePdfRenderService();
        var viewModel = CreateViewModelWithServices(render: render);
        viewModel.SourceFiles.Add(sourceFile);

        await viewModel.OpenPreviewCommand.ExecuteAsync(sourceFile.Pages[0]);
        await viewModel.PreviewZoomInCommand.ExecuteAsync(null);
        await viewModel.SetPreviewFitModeCommand.ExecuteAsync(PreviewFitMode.FitHeight);

        Assert.Equal([1100, 990, 675], render.PixelWidths);
        Assert.Equal(PreviewFitMode.FitHeight, viewModel.PreviewFitMode);
    }

    [Fact]
    public async Task TogglePreviewSelection_updates_source_selection_and_statistics()
    {
        var sourceFile = CreateSourceFile(@"D:\Docs\a.pdf", 1);
        var viewModel = CreateViewModelWith(sourceFile);
        await viewModel.OpenPreviewCommand.ExecuteAsync(sourceFile.Pages[0]);

        viewModel.TogglePreviewSelectionCommand.Execute(null);

        Assert.True(sourceFile.Pages[0].IsSelected);
        Assert.Equal(1, viewModel.SelectedPageCount);

        viewModel.TogglePreviewSelectionCommand.Execute(null);

        Assert.False(sourceFile.Pages[0].IsSelected);
        Assert.Equal(0, viewModel.SelectedPageCount);
    }

    [Fact]
    public async Task ShowPreviewGrid_exposes_all_pages_from_current_pdf_and_allows_selection()
    {
        var sourceFile = CreateSourceFile(@"D:\Docs\a.pdf", 3);
        var viewModel = CreateViewModelWith(sourceFile);
        await viewModel.OpenPreviewCommand.ExecuteAsync(sourceFile.Pages[1]);

        Assert.Empty(viewModel.PreviewGridPages);

        viewModel.ShowPreviewGridCommand.Execute(null);
        viewModel.SelectPageCommand.Execute(new PageSelectionRequest(sourceFile.Pages[2], SelectionGesture.Toggle));

        Assert.True(viewModel.IsPreviewGridOpen);
        Assert.Same(sourceFile, viewModel.PreviewSourceFile);
        Assert.Equal([1, 2, 3], viewModel.PreviewSourcePages.Select(page => page.PageNumber));
        Assert.Equal([1, 2, 3], viewModel.PreviewGridPages.Select(page => page.PageNumber));
        Assert.True(sourceFile.Pages[2].IsSelected);
        Assert.Equal(1, viewModel.SelectedPageCount);
        Assert.Contains("tat ca trang", viewModel.PreviewTitle);
    }

    [Fact]
    public async Task ShowSinglePreview_returns_from_grid_to_active_page_preview()
    {
        var sourceFile = CreateSourceFile(@"D:\Docs\a.pdf", 2);
        var render = new FakePdfRenderService();
        var viewModel = CreateViewModelWithServices(render: render);
        viewModel.SourceFiles.Add(sourceFile);
        await viewModel.OpenPreviewCommand.ExecuteAsync(sourceFile.Pages[0]);
        viewModel.ShowPreviewGridCommand.Execute(null);

        await viewModel.ShowSinglePreviewCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsPreviewGridOpen);
        Assert.Same(sourceFile.Pages[0], viewModel.PreviewPage);
        Assert.NotNull(viewModel.PreviewImage);
        Assert.Equal([1100, 1100], render.PixelWidths);
    }

    [Fact]
    public async Task CheckRecoveryOnStartupAsync_reports_available_recovery()
    {
        var autoSave = new FakeAutoSaveService { HasRecovery = true };
        var viewModel = CreateViewModelWithServices(autoSave: autoSave);

        await viewModel.CheckRecoveryOnStartupAsync(CancellationToken.None);

        Assert.True(viewModel.HasRecoverySession);
        Assert.Equal("Phát hiện phiên làm việc chưa phục hồi", viewModel.RecoveryStatus);
    }

    [Fact]
    public async Task RecoverSessionCommand_restores_sources_output_and_marks_missing_files()
    {
        var sourceFileId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var autoSave = new FakeAutoSaveService
        {
            HasRecovery = true,
            RecoveryState = new ProjectState
            {
                SourceFiles =
                [
                    new ProjectSourceFile
                    {
                        Id = sourceFileId,
                        FilePath = @"D:\Missing\a.pdf",
                        DisplayName = "a.pdf",
                        PageCount = 2,
                        FileSize = 10,
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
                                SourcePageNumber = 2
                            }
                        ]
                    }
                ],
                UiState = new ProjectUiState { ThumbnailZoom = 1.25 }
            }
        };
        var viewModel = CreateViewModelWithServices(autoSave: autoSave);

        await viewModel.RecoverSessionCommand.ExecuteAsync(null);

        Assert.Single(viewModel.SourceFiles);
        Assert.True(viewModel.SourceFiles[0].IsMissing);
        Assert.Equal(1.25, viewModel.ThumbnailZoom);
        Assert.Single(viewModel.OutputGroups);
        Assert.Equal(2, viewModel.OutputGroups[0].Items.Single().SourcePageNumber);
        Assert.Equal(1, viewModel.SourceFiles[0].Pages[1].OutputOccurrenceCount);
        Assert.False(viewModel.HasRecoverySession);
    }

    [Fact]
    public async Task RelinkSourceFileAsync_updates_missing_source_without_changing_output_references()
    {
        var sourceFile = CreateSourceFile(@"D:\Old\a.pdf", 2);
        sourceFile.IsMissing = true;
        var replacement = CreateSourceFile(@"D:\New\a.pdf", 2);
        var metadata = new FakePdfMetadataService();
        metadata.Add(replacement.FilePath, replacement);
        var viewModel = CreateViewModelWithServices(metadata: metadata);
        viewModel.SourceFiles.Add(sourceFile);
        SelectPagesAndAddToOutput(viewModel, sourceFile, 2);
        var outputItem = viewModel.OutputGroups.Single().Items.Single();

        await viewModel.RelinkSourceFileAsync(sourceFile, replacement.FilePath, CancellationToken.None);

        Assert.False(sourceFile.IsMissing);
        Assert.Equal(replacement.FilePath, sourceFile.FilePath);
        Assert.Equal(sourceFile.Id, outputItem.SourceFileId);
        Assert.Equal(2, outputItem.SourcePageNumber);
        Assert.Equal("Đã liên kết lại a.pdf", viewModel.RenderStatus);
    }

    [Fact]
    public async Task RelinkSourceFileAsync_rejects_page_count_or_fingerprint_mismatch()
    {
        var sourceFile = CreateSourceFile(@"D:\Old\a.pdf", 2);
        sourceFile.IsMissing = true;
        var metadata = new FakePdfMetadataService();
        metadata.Add(@"D:\New\wrong-pages.pdf", CreateSourceFile(@"D:\New\wrong-pages.pdf", 3));
        metadata.Add(@"D:\New\wrong-fingerprint.pdf", new SourcePdfFile(
            Guid.NewGuid(),
            @"D:\New\wrong-fingerprint.pdf",
            "wrong-fingerprint.pdf",
            2,
            2048,
            "other-fingerprint"));
        var viewModel = CreateViewModelWithServices(metadata: metadata);
        viewModel.SourceFiles.Add(sourceFile);

        await viewModel.RelinkSourceFileAsync(sourceFile, @"D:\New\wrong-pages.pdf", CancellationToken.None);

        Assert.True(sourceFile.IsMissing);
        Assert.Equal(@"D:\Old\a.pdf", sourceFile.FilePath);
        Assert.Equal("File liên kết lại không khớp số trang", viewModel.RenderStatus);

        await viewModel.RelinkSourceFileAsync(sourceFile, @"D:\New\wrong-fingerprint.pdf", CancellationToken.None);

        Assert.True(sourceFile.IsMissing);
        Assert.Equal(@"D:\Old\a.pdf", sourceFile.FilePath);
        Assert.Equal("File liên kết lại không khớp fingerprint", viewModel.RenderStatus);
    }

    [Fact]
    public async Task ImportPdfFilesAsync_imports_500_pages_without_eager_thumbnail_rendering()
    {
        var metadata = new FakePdfMetadataService();
        var paths = Enumerable.Range(1, 10)
            .Select(index => $@"D:\Docs\file-{index}.pdf")
            .ToList();
        foreach (var path in paths)
        {
            metadata.Add(path, CreateSourceFile(path, 50));
        }

        var render = new FakePdfRenderService();
        var viewModel = CreateViewModelWithServices(metadata: metadata, render: render);
        var stopwatch = Stopwatch.StartNew();

        await viewModel.ImportPdfFilesAsync(paths, CancellationToken.None);
        stopwatch.Stop();

        Assert.Equal(10, viewModel.SourceFileCount);
        Assert.Equal(500, viewModel.SourcePageCount);
        Assert.Equal(0, render.CallCount);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2), $"Import took {stopwatch.Elapsed}.");
    }

    [Fact]
    public void SourceSearchText_filters_source_files_by_display_name()
    {
        var first = CreateSourceFile(@"D:\Docs\alpha.pdf", 1);
        var second = CreateSourceFile(@"D:\Docs\beta.pdf", 1);
        var viewModel = CreateViewModelWith(first, second);

        viewModel.SourceSearchText = "alp";

        Assert.Equal([first], viewModel.FilteredSourceFiles);
        Assert.True(viewModel.HasSourceSearch);

        viewModel.ClearSourceSearchCommand.Execute(null);

        Assert.Equal([first, second], viewModel.FilteredSourceFiles);
        Assert.False(viewModel.HasSourceSearch);
    }

    [Fact]
    public void SelectPageCommand_toggles_page_without_changing_output_badge()
    {
        var sourceFile = CreateSourceFile(@"D:\Docs\a.pdf", 1);
        var page = sourceFile.Pages[0];
        page.OutputOccurrenceCount = 2;
        var viewModel = CreateViewModelWith(sourceFile);

        viewModel.SelectPageCommand.Execute(new PageSelectionRequest(page, SelectionGesture.Toggle));

        Assert.True(page.IsSelected);
        Assert.Equal(2, page.OutputOccurrenceCount);
        Assert.Equal(1, viewModel.SelectedPageCount);

        viewModel.SelectPageCommand.Execute(new PageSelectionRequest(page, SelectionGesture.Toggle));

        Assert.False(page.IsSelected);
        Assert.Equal(2, page.OutputOccurrenceCount);
        Assert.Equal(0, viewModel.SelectedPageCount);
    }

    [Fact]
    public void SelectPageCommand_ctrl_click_toggles_discrete_pages()
    {
        var sourceFile = CreateSourceFile(@"D:\Docs\a.pdf", 3);
        var viewModel = CreateViewModelWith(sourceFile);

        viewModel.SelectPageCommand.Execute(new PageSelectionRequest(sourceFile.Pages[0], SelectionGesture.AddOrRemove));
        viewModel.SelectPageCommand.Execute(new PageSelectionRequest(sourceFile.Pages[2], SelectionGesture.AddOrRemove));

        Assert.Equal([1, 3], sourceFile.Pages.Where(page => page.IsSelected).Select(page => page.PageNumber));
        Assert.Equal(2, viewModel.SelectedPageCount);
    }

    [Fact]
    public void SelectPageCommand_shift_click_selects_range_in_same_file()
    {
        var sourceFile = CreateSourceFile(@"D:\Docs\a.pdf", 5);
        var viewModel = CreateViewModelWith(sourceFile);

        viewModel.SelectPageCommand.Execute(new PageSelectionRequest(sourceFile.Pages[1], SelectionGesture.Toggle));
        viewModel.SelectPageCommand.Execute(new PageSelectionRequest(sourceFile.Pages[4], SelectionGesture.Range));

        Assert.Equal([2, 3, 4, 5], sourceFile.Pages.Where(page => page.IsSelected).Select(page => page.PageNumber));
    }

    [Fact]
    public void SelectPageCommand_shift_click_does_not_select_other_file_range()
    {
        var first = CreateSourceFile(@"D:\Docs\a.pdf", 3);
        var second = CreateSourceFile(@"D:\Docs\b.pdf", 3);
        var viewModel = CreateViewModelWith(first, second);

        viewModel.SelectPageCommand.Execute(new PageSelectionRequest(first.Pages[0], SelectionGesture.Toggle));
        viewModel.SelectPageCommand.Execute(new PageSelectionRequest(second.Pages[2], SelectionGesture.Range));

        Assert.Equal([1], first.Pages.Where(page => page.IsSelected).Select(page => page.PageNumber));
        Assert.Equal([3], second.Pages.Where(page => page.IsSelected).Select(page => page.PageNumber));
    }

    [Fact]
    public void File_selection_commands_select_and_clear_only_that_file()
    {
        var first = CreateSourceFile(@"D:\Docs\a.pdf", 2);
        var second = CreateSourceFile(@"D:\Docs\b.pdf", 2);
        var viewModel = CreateViewModelWith(first, second);

        viewModel.SelectAllInFileCommand.Execute(first);

        Assert.All(first.Pages, page => Assert.True(page.IsSelected));
        Assert.All(second.Pages, page => Assert.False(page.IsSelected));
        Assert.Equal(2, viewModel.SelectedPageCount);

        viewModel.ClearSelectionInFileCommand.Execute(first);

        Assert.All(first.Pages, page => Assert.False(page.IsSelected));
        Assert.Equal(0, viewModel.SelectedPageCount);
    }

    [Fact]
    public void ToolbarSelectionCommands_select_and_clear_all_pages()
    {
        var first = CreateSourceFile(@"D:\Docs\a.pdf", 2);
        var second = CreateSourceFile(@"D:\Docs\b.pdf", 3);
        var viewModel = CreateViewModelWith(first, second);

        viewModel.SelectAllPagesCommand.Execute(null);

        Assert.Equal(5, viewModel.SelectedPageCount);
        Assert.All(first.Pages.Concat(second.Pages), page => Assert.True(page.IsSelected));

        viewModel.ClearAllSelectionCommand.Execute(null);

        Assert.Equal(0, viewModel.SelectedPageCount);
        Assert.All(first.Pages.Concat(second.Pages), page => Assert.False(page.IsSelected));
    }

    [Fact]
    public void AddSelectionToOutputCommand_creates_group_in_file_then_page_order()
    {
        var first = CreateSourceFile(@"D:\Docs\a.pdf", 4);
        var second = CreateSourceFile(@"D:\Docs\b.pdf", 3);
        var viewModel = CreateViewModelWith(first, second);
        first.Pages[3].IsSelected = true;
        first.Pages[1].IsSelected = true;
        second.Pages[2].IsSelected = true;
        second.Pages[0].IsSelected = true;

        viewModel.AddSelectionToOutputCommand.Execute(null);

        var group = Assert.Single(viewModel.OutputGroups);
        Assert.Equal([2, 4, 1, 3], group.Items.Select(item => item.SourcePageNumber));
        Assert.Equal([first.Id, first.Id, second.Id, second.Id], group.Items.Select(item => item.SourceFileId));
        Assert.Equal(4, viewModel.OutputPageCount);
        Assert.Equal("Đã thêm 4 trang vào đầu ra", viewModel.RenderStatus);
    }

    [Fact]
    public void AddSelectionToOutputCommand_does_not_clear_selection_and_updates_occurrences()
    {
        var sourceFile = CreateSourceFile(@"D:\Docs\a.pdf", 2);
        var viewModel = CreateViewModelWith(sourceFile);
        sourceFile.Pages[0].IsSelected = true;

        viewModel.AddSelectionToOutputCommand.Execute(null);
        viewModel.AddSelectionToOutputCommand.Execute(null);

        Assert.True(sourceFile.Pages[0].IsSelected);
        Assert.Equal(2, sourceFile.Pages[0].OutputOccurrenceCount);
        Assert.Equal(2, viewModel.OutputGroups.Count);
        Assert.Equal(2, viewModel.OutputPageCount);
    }

    [Fact]
    public void AddSelectionToOutputCommand_creates_independent_output_item_ids()
    {
        var sourceFile = CreateSourceFile(@"D:\Docs\a.pdf", 1);
        var viewModel = CreateViewModelWith(sourceFile);
        sourceFile.Pages[0].IsSelected = true;

        viewModel.AddSelectionToOutputCommand.Execute(null);
        viewModel.AddSelectionToOutputCommand.Execute(null);

        var items = viewModel.OutputGroups.SelectMany(group => group.Items).ToList();
        Assert.Equal(2, items.Select(item => item.Id).Distinct().Count());
        Assert.All(items, item => Assert.Equal(sourceFile.Id, item.SourceFileId));
        Assert.All(items, item => Assert.Equal(1, item.SourcePageNumber));
    }

    [Fact]
    public void OutputTrayItems_returns_continuous_indices()
    {
        var sourceFile = CreateSourceFile(@"D:\Docs\a.pdf", 2);
        var viewModel = CreateViewModelWith(sourceFile);
        sourceFile.Pages[0].IsSelected = true;
        sourceFile.Pages[1].IsSelected = true;

        viewModel.AddSelectionToOutputCommand.Execute(null);

        Assert.Equal([1, 2], viewModel.OutputTrayItems.Select(item => item.OutputIndex));
        Assert.Equal(["a.pdf", "a.pdf"], viewModel.OutputTrayItems.Select(item => item.SourceDisplayName));
        Assert.Same(sourceFile.Pages[0], viewModel.OutputTrayItems.First().SourcePage);
    }

    [Fact]
    public void DeleteOutputItemCommand_removes_item_and_recalculates_badges()
    {
        var sourceFile = CreateSourceFile(@"D:\Docs\a.pdf", 2);
        var viewModel = CreateViewModelWith(sourceFile);
        sourceFile.Pages[0].IsSelected = true;
        sourceFile.Pages[1].IsSelected = true;
        viewModel.AddSelectionToOutputCommand.Execute(null);
        var firstItem = viewModel.OutputTrayItems.First();

        viewModel.DeleteOutputItemCommand.Execute(firstItem);

        Assert.Single(viewModel.OutputGroups);
        Assert.Equal([1], viewModel.OutputTrayItems.Select(item => item.OutputIndex));
        Assert.Equal(0, sourceFile.Pages[0].OutputOccurrenceCount);
        Assert.Equal(1, sourceFile.Pages[1].OutputOccurrenceCount);
        Assert.Equal(1, viewModel.OutputPageCount);
    }

    [Fact]
    public void DeleteOutputItemCommand_removes_empty_group()
    {
        var sourceFile = CreateSourceFile(@"D:\Docs\a.pdf", 1);
        var viewModel = CreateViewModelWith(sourceFile);
        sourceFile.Pages[0].IsSelected = true;
        viewModel.AddSelectionToOutputCommand.Execute(null);

        viewModel.DeleteOutputItemCommand.Execute(viewModel.OutputTrayItems.Single());

        Assert.Empty(viewModel.OutputGroups);
        Assert.Equal(0, sourceFile.Pages[0].OutputOccurrenceCount);
        Assert.Equal(0, viewModel.OutputPageCount);
    }

    [Fact]
    public void DeleteOutputItemsCommand_removes_multiple_selected_items()
    {
        var sourceFile = CreateSourceFile(@"D:\Docs\a.pdf", 3);
        var viewModel = CreateViewModelWith(sourceFile);
        sourceFile.Pages[0].IsSelected = true;
        sourceFile.Pages[1].IsSelected = true;
        sourceFile.Pages[2].IsSelected = true;
        viewModel.AddSelectionToOutputCommand.Execute(null);
        var selectedItems = viewModel.OutputTrayItems.Take(2).ToArray();

        viewModel.DeleteOutputItemsCommand.Execute(selectedItems);

        Assert.Equal([3], viewModel.OutputGroups.Single().Items.Select(item => item.SourcePageNumber));
        Assert.Equal([0, 0, 1], sourceFile.Pages.Select(page => page.OutputOccurrenceCount));
        Assert.Equal(1, viewModel.OutputPageCount);
    }

    [Fact]
    public void DeleteOutputGroupCommand_removes_whole_group_and_recalculates_badges()
    {
        var sourceFile = CreateSourceFile(@"D:\Docs\a.pdf", 2);
        var viewModel = CreateViewModelWith(sourceFile);
        sourceFile.Pages[0].IsSelected = true;
        sourceFile.Pages[1].IsSelected = true;
        viewModel.AddSelectionToOutputCommand.Execute(null);
        var group = viewModel.OutputGroups.Single();

        viewModel.DeleteOutputGroupCommand.Execute(group);

        Assert.Empty(viewModel.OutputGroups);
        Assert.All(sourceFile.Pages, page => Assert.Equal(0, page.OutputOccurrenceCount));
    }

    [Fact]
    public void DuplicateOutputItemCommand_inserts_copy_after_original()
    {
        var sourceFile = CreateSourceFile(@"D:\Docs\a.pdf", 2);
        var viewModel = CreateViewModelWith(sourceFile);
        sourceFile.Pages[0].IsSelected = true;
        sourceFile.Pages[1].IsSelected = true;
        viewModel.AddSelectionToOutputCommand.Execute(null);
        var originalView = viewModel.OutputTrayItems.First();
        var originalId = originalView.Item.Id;

        viewModel.DuplicateOutputItemCommand.Execute(originalView);

        var items = viewModel.OutputGroups.Single().Items;
        Assert.Equal([1, 1, 2], items.Select(item => item.SourcePageNumber));
        Assert.NotEqual(originalId, items[1].Id);
        Assert.Equal(2, sourceFile.Pages[0].OutputOccurrenceCount);
        Assert.Equal(3, viewModel.OutputPageCount);
    }

    [Fact]
    public void DuplicateOutputItemsCommand_duplicates_multiple_items_in_place()
    {
        var sourceFile = CreateSourceFile(@"D:\Docs\a.pdf", 3);
        var viewModel = CreateViewModelWith(sourceFile);
        sourceFile.Pages[0].IsSelected = true;
        sourceFile.Pages[1].IsSelected = true;
        sourceFile.Pages[2].IsSelected = true;
        viewModel.AddSelectionToOutputCommand.Execute(null);
        var selectedViews = viewModel.OutputTrayItems.Take(2).ToArray();

        viewModel.DuplicateOutputItemsCommand.Execute(selectedViews);

        var items = viewModel.OutputGroups.Single().Items;
        Assert.Equal([1, 1, 2, 2, 3], items.Select(item => item.SourcePageNumber));
        Assert.Equal(2, sourceFile.Pages[0].OutputOccurrenceCount);
        Assert.Equal(2, sourceFile.Pages[1].OutputOccurrenceCount);
        Assert.Equal(1, sourceFile.Pages[2].OutputOccurrenceCount);
    }


    [Fact]
    public void DuplicateOutputGroupCommand_copies_items_with_new_group_and_item_ids()
    {
        var sourceFile = CreateSourceFile(@"D:\Docs\a.pdf", 3);
        var viewModel = CreateViewModelWith(sourceFile);
        sourceFile.Pages[0].IsSelected = true;
        sourceFile.Pages[1].IsSelected = true;
        sourceFile.Pages[2].IsSelected = true;
        viewModel.AddSelectionToOutputCommand.Execute(null);
        var original = viewModel.OutputGroups.Single();

        viewModel.DuplicateOutputGroupCommand.Execute(original);

        Assert.Equal(2, viewModel.OutputGroups.Count);
        var duplicate = viewModel.OutputGroups[1];
        Assert.NotEqual(original.Id, duplicate.Id);
        Assert.Equal([1, 2, 3], duplicate.Items.Select(item => item.SourcePageNumber));
        Assert.Empty(original.Items.Select(item => item.Id).Intersect(duplicate.Items.Select(item => item.Id)));
        Assert.All(sourceFile.Pages, page => Assert.Equal(2, page.OutputOccurrenceCount));
    }

    [Fact]
    public void MoveOutputItemDownCommand_reorders_item_within_group()
    {
        var sourceFile = CreateSourceFile(@"D:\Docs\a.pdf", 3);
        var viewModel = CreateViewModelWith(sourceFile);
        SelectPagesAndAddToOutput(viewModel, sourceFile, 1, 2, 3);

        viewModel.MoveOutputItemDownCommand.Execute(viewModel.OutputTrayItems.First());

        Assert.Equal([2, 1, 3], viewModel.OutputGroups.Single().Items.Select(item => item.SourcePageNumber));
        Assert.Equal([1, 2, 3], viewModel.OutputTrayItems.Select(item => item.OutputIndex));
    }

    [Fact]
    public void MoveOutputItemBeforeCommand_reorders_dragged_item_before_drop_target()
    {
        var sourceFile = CreateSourceFile(@"D:\Docs\a.pdf", 3);
        var viewModel = CreateViewModelWith(sourceFile);
        SelectPagesAndAddToOutput(viewModel, sourceFile, 1, 2, 3);
        var source = viewModel.OutputTrayItems.ElementAt(2).Item;
        var target = viewModel.OutputTrayItems.ElementAt(0).Item;

        viewModel.MoveOutputItemBeforeCommand.Execute(new OutputItemMoveRequest(source, target));

        Assert.Equal([3, 1, 2], viewModel.OutputGroups.Single().Items.Select(item => item.SourcePageNumber));
        Assert.All(sourceFile.Pages, page => Assert.Equal(1, page.OutputOccurrenceCount));
    }

    [Fact]
    public void MoveOutputItemDownCommand_moves_item_across_group_boundary()
    {
        var sourceFile = CreateSourceFile(@"D:\Docs\a.pdf", 3);
        var viewModel = CreateViewModelWith(sourceFile);
        SelectPagesAndAddToOutput(viewModel, sourceFile, 1);
        sourceFile.Pages[0].IsSelected = false;
        SelectPagesAndAddToOutput(viewModel, sourceFile, 2, 3);
        var firstItemView = viewModel.OutputTrayItems.First();

        viewModel.MoveOutputItemDownCommand.Execute(firstItemView);

        Assert.Single(viewModel.OutputGroups);
        Assert.Equal([2, 1, 3], viewModel.OutputGroups.Single().Items.Select(item => item.SourcePageNumber));
        Assert.Equal(viewModel.OutputGroups.Single().Id, viewModel.OutputGroups.Single().Items[1].GroupId);
    }

    [Fact]
    public void MoveOutputGroupDownCommand_moves_whole_group_without_changing_internal_order()
    {
        var sourceFile = CreateSourceFile(@"D:\Docs\a.pdf", 4);
        var viewModel = CreateViewModelWith(sourceFile);
        SelectPagesAndAddToOutput(viewModel, sourceFile, 1, 2);
        sourceFile.Pages[0].IsSelected = false;
        sourceFile.Pages[1].IsSelected = false;
        SelectPagesAndAddToOutput(viewModel, sourceFile, 3, 4);
        var firstGroup = viewModel.OutputGroups[0];

        viewModel.MoveOutputGroupDownCommand.Execute(firstGroup);

        Assert.Equal([3, 4, 1, 2], viewModel.OutputTrayItems.Select(item => item.SourcePageNumber));
        Assert.Equal([1, 2], viewModel.OutputGroups[1].Items.Select(item => item.SourcePageNumber));
    }

    [Fact]
    public void MoveOutputGroupBeforeCommand_reorders_whole_group_before_target()
    {
        var sourceFile = CreateSourceFile(@"D:\Docs\a.pdf", 4);
        var viewModel = CreateViewModelWith(sourceFile);
        SelectPagesAndAddToOutput(viewModel, sourceFile, 1, 2);
        sourceFile.Pages[0].IsSelected = false;
        sourceFile.Pages[1].IsSelected = false;
        SelectPagesAndAddToOutput(viewModel, sourceFile, 3, 4);
        var firstGroup = viewModel.OutputGroups[0];
        var secondGroup = viewModel.OutputGroups[1];

        viewModel.MoveOutputGroupBeforeCommand.Execute(new OutputGroupMoveRequest(secondGroup, firstGroup));

        Assert.Equal(secondGroup.Id, viewModel.OutputGroups[0].Id);
        Assert.Equal([3, 4, 1, 2], viewModel.OutputTrayItems.Select(item => item.SourcePageNumber));
        Assert.Equal([3, 4], viewModel.OutputGroups[0].Items.Select(item => item.SourcePageNumber));
    }

    [Fact]
    public void UndoRedo_restores_add_output_state_and_badges()
    {
        var sourceFile = CreateSourceFile(@"D:\Docs\a.pdf", 1);
        var viewModel = CreateViewModelWith(sourceFile);
        sourceFile.Pages[0].IsSelected = true;

        viewModel.AddSelectionToOutputCommand.Execute(null);
        viewModel.UndoCommand.Execute(null);

        Assert.Empty(viewModel.OutputGroups);
        Assert.Equal(0, sourceFile.Pages[0].OutputOccurrenceCount);
        Assert.True(viewModel.CanRedo);

        viewModel.RedoCommand.Execute(null);

        Assert.Single(viewModel.OutputGroups);
        Assert.Equal(1, sourceFile.Pages[0].OutputOccurrenceCount);
    }

    [Fact]
    public void UndoRedo_restores_delete_duplicate_and_reorder_state()
    {
        var sourceFile = CreateSourceFile(@"D:\Docs\a.pdf", 3);
        var viewModel = CreateViewModelWith(sourceFile);
        SelectPagesAndAddToOutput(viewModel, sourceFile, 1, 2, 3);
        var firstView = viewModel.OutputTrayItems.First();

        viewModel.DeleteOutputItemCommand.Execute(firstView);
        viewModel.UndoCommand.Execute(null);
        Assert.Equal([1, 2, 3], viewModel.OutputGroups.Single().Items.Select(item => item.SourcePageNumber));

        viewModel.DuplicateOutputItemCommand.Execute(viewModel.OutputTrayItems.First());
        viewModel.UndoCommand.Execute(null);
        Assert.Equal([1, 2, 3], viewModel.OutputGroups.Single().Items.Select(item => item.SourcePageNumber));

        viewModel.MoveOutputItemDownCommand.Execute(viewModel.OutputTrayItems.First());
        Assert.Equal([2, 1, 3], viewModel.OutputGroups.Single().Items.Select(item => item.SourcePageNumber));
        viewModel.UndoCommand.Execute(null);
        Assert.Equal([1, 2, 3], viewModel.OutputGroups.Single().Items.Select(item => item.SourcePageNumber));
    }

    [Fact]
    public void UndoRedo_restores_bulk_selection_state()
    {
        var sourceFile = CreateSourceFile(@"D:\Docs\a.pdf", 2);
        var viewModel = CreateViewModelWith(sourceFile);

        viewModel.SelectAllInFileCommand.Execute(sourceFile);
        viewModel.UndoCommand.Execute(null);

        Assert.All(sourceFile.Pages, page => Assert.False(page.IsSelected));
        Assert.Equal(0, viewModel.SelectedPageCount);

        viewModel.RedoCommand.Execute(null);

        Assert.All(sourceFile.Pages, page => Assert.True(page.IsSelected));
        Assert.Equal(2, viewModel.SelectedPageCount);
    }

    [Fact]
    public void UndoRedo_clears_redo_when_new_branch_operation_occurs()
    {
        var sourceFile = CreateSourceFile(@"D:\Docs\a.pdf", 2);
        var viewModel = CreateViewModelWith(sourceFile);
        SelectPagesAndAddToOutput(viewModel, sourceFile, 1);
        viewModel.UndoCommand.Execute(null);
        Assert.True(viewModel.CanRedo);

        sourceFile.Pages[0].IsSelected = false;
        sourceFile.Pages[1].IsSelected = true;
        viewModel.AddSelectionToOutputCommand.Execute(null);

        Assert.False(viewModel.CanRedo);
        Assert.Equal([2], viewModel.OutputGroups.Single().Items.Select(item => item.SourcePageNumber));
    }

    [Fact]
    public void ToggleOutputGroupCollapseCommand_hides_items_without_changing_output_count()
    {
        var sourceFile = CreateSourceFile(@"D:\Docs\a.pdf", 2);
        var viewModel = CreateViewModelWith(sourceFile);
        SelectPagesAndAddToOutput(viewModel, sourceFile, 1, 2);
        var group = viewModel.OutputGroups.Single();

        viewModel.ToggleOutputGroupCollapseCommand.Execute(group);

        Assert.True(group.IsCollapsed);
        Assert.Empty(viewModel.OutputTrayItems);
        Assert.Equal(2, viewModel.OutputPageCount);
    }

    [Fact]
    public async Task PreviewOutputCommand_previews_output_pages_in_tray_order()
    {
        var sourceFile = CreateSourceFile(@"D:\Docs\a.pdf", 2);
        var viewModel = CreateViewModelWith(sourceFile);
        SelectPagesAndAddToOutput(viewModel, sourceFile, 2);
        sourceFile.Pages[1].IsSelected = false;
        SelectPagesAndAddToOutput(viewModel, sourceFile, 1);
        viewModel.ClearAllSelectionCommand.Execute(null);

        await viewModel.PreviewOutputCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsPreviewOpen);
        Assert.True(viewModel.IsOutputPreviewMode);
        Assert.True(viewModel.IsPreviewGridOpen);
        Assert.Same(sourceFile.Pages[1], viewModel.PreviewPage);
        Assert.Contains("2 trang", viewModel.PreviewTitle);
        Assert.Equal([2, 1], viewModel.OutputPreviewGridItems.Select(item => item.SourcePageNumber));
        Assert.Equal([2, 1], viewModel.OutputPreviewGridPages.Single().Select(item => item.SourcePageNumber));
        Assert.False(sourceFile.Pages[0].IsSelected);
        Assert.False(sourceFile.Pages[1].IsSelected);

        Assert.Contains("tất cả 2 trang", viewModel.PreviewTitle);

        await viewModel.OpenOutputPreviewItemCommand.ExecuteAsync(viewModel.OutputPreviewGridItems.First());

        Assert.False(viewModel.IsPreviewGridOpen);
        Assert.Same(sourceFile.Pages[1], viewModel.PreviewPage);
        Assert.Contains("1/2", viewModel.PreviewTitle);

        await viewModel.PreviewNextCommand.ExecuteAsync(null);

        Assert.Same(sourceFile.Pages[0], viewModel.PreviewPage);
        Assert.Contains("2/2", viewModel.PreviewTitle);
        Assert.Contains("trang 1", viewModel.PreviewTitle);
    }

    [Fact]
    public async Task OutputPreviewGridPages_groups_pages_into_two_rows_of_seven()
    {
        var sourceFile = CreateSourceFile(@"D:\Docs\a.pdf", 33);
        var viewModel = CreateViewModelWith(sourceFile);
        SelectPagesAndAddToOutput(viewModel, sourceFile, Enumerable.Range(1, 33).ToArray());

        await viewModel.PreviewOutputCommand.ExecuteAsync(null);

        var pages = viewModel.OutputPreviewGridPages.ToList();

        Assert.Equal([14, 14, 5], pages.Select(page => page.Count));
        Assert.Equal(Enumerable.Range(1, 14), pages[0].Select(item => item.OutputIndex));
        Assert.Equal(Enumerable.Range(15, 14), pages[1].Select(item => item.OutputIndex));
        Assert.Equal(Enumerable.Range(29, 5), pages[2].Select(item => item.OutputIndex));
        Assert.Equal(3, viewModel.OutputPreviewGridPageCount);
        Assert.Equal(Enumerable.Range(1, 14), viewModel.CurrentOutputPreviewGridItems.Select(item => item.OutputIndex));

        viewModel.MoveOutputPreviewGridPage(1);

        Assert.Equal(1, viewModel.OutputPreviewGridPageIndex);
        Assert.Equal(Enumerable.Range(15, 14), viewModel.CurrentOutputPreviewGridItems.Select(item => item.OutputIndex));

        viewModel.MoveOutputPreviewGridPage(99);

        Assert.Equal(2, viewModel.OutputPreviewGridPageIndex);
        Assert.Equal(Enumerable.Range(29, 5), viewModel.CurrentOutputPreviewGridItems.Select(item => item.OutputIndex));
    }

    [Fact]
    public async Task ExportPdfCommand_exports_output_and_opens_exported_file()
    {
        var sourceFile = CreateSourceFile(@"D:\Docs\a.pdf", 2);
        var export = new FakePdfExportService();
        var foxit = new FakeFoxitLauncherService();
        var dialog = new FakeFileDialogService([], outputPath: @"D:\Out\merged.pdf");
        var viewModel = CreateViewModelWithServices(fileDialog: dialog, export: export, foxit: foxit);
        viewModel.SourceFiles.Add(sourceFile);
        sourceFile.Pages[0].IsSelected = true;
        viewModel.AddSelectionToOutputCommand.Execute(null);

        await viewModel.ExportPdfCommand.ExecuteAsync(null);

        Assert.Equal(@"D:\Out\merged.pdf", export.OutputPath);
        Assert.Equal(1, export.SourceFileCount);
        Assert.Equal(1, export.GroupCount);
        Assert.Equal(@"D:\Out\merged.pdf", foxit.OpenedPath);
        Assert.Equal("Đã xuất PDF: merged.pdf", viewModel.RenderStatus);
        Assert.False(viewModel.IsExporting);
        Assert.Equal(100, viewModel.ExportProgressPercent);
    }

    [Fact]
    public async Task ExportPdfCommand_does_not_throw_when_opening_exported_file_fails()
    {
        var sourceFile = CreateSourceFile(@"D:\Docs\a.pdf", 1);
        var export = new FakePdfExportService();
        var foxit = new FakeFoxitLauncherService { OpenException = new UnauthorizedAccessException("blocked") };
        var dialog = new FakeFileDialogService([], outputPath: @"D:\Out\merged.pdf");
        var viewModel = CreateViewModelWithServices(fileDialog: dialog, export: export, foxit: foxit);
        viewModel.SourceFiles.Add(sourceFile);
        sourceFile.Pages[0].IsSelected = true;
        viewModel.AddSelectionToOutputCommand.Execute(null);

        await viewModel.ExportPdfCommand.ExecuteAsync(null);

        Assert.Equal(@"D:\Out\merged.pdf", export.OutputPath);
        Assert.Equal("Đã xuất PDF nhưng không mở được file: merged.pdf", viewModel.RenderStatus);
    }


    [Fact]
    public async Task ExportPdfCommand_reports_empty_output_without_dialog()
    {
        var export = new FakePdfExportService();
        var viewModel = CreateViewModelWithServices(export: export);

        await viewModel.ExportPdfCommand.ExecuteAsync(null);

        Assert.Null(export.OutputPath);
        Assert.Equal("Khay đầu ra đang trống", viewModel.RenderStatus);
    }

    [Fact]
    public void ToggleSourceFileCollapseCommand_does_not_change_selection_or_output()
    {
        var sourceFile = CreateSourceFile(@"D:\Docs\a.pdf", 2);
        var viewModel = CreateViewModelWith(sourceFile);
        sourceFile.Pages[0].IsSelected = true;
        viewModel.AddSelectionToOutputCommand.Execute(null);

        viewModel.ToggleSourceFileCollapseCommand.Execute(sourceFile);

        Assert.True(sourceFile.IsCollapsed);
        Assert.True(sourceFile.Pages[0].IsSelected);
        Assert.Equal(1, sourceFile.Pages[0].OutputOccurrenceCount);
        Assert.Equal(1, viewModel.OutputPageCount);

        viewModel.ToggleSourceFileCollapseCommand.Execute(sourceFile);

        Assert.False(sourceFile.IsCollapsed);
    }

    [Fact]
    public void AdjustThumbnailZoom_clamps_and_updates_card_dimensions()
    {
        var viewModel = CreateViewModelWith();
        var originalWidth = viewModel.ThumbnailCardWidth;

        viewModel.AdjustThumbnailZoom(0.3);

        Assert.Equal(1.3, viewModel.ThumbnailZoom, precision: 3);
        Assert.True(viewModel.ThumbnailCardWidth > originalWidth);

        viewModel.AdjustThumbnailZoom(99);
        Assert.Equal(1.6, viewModel.ThumbnailZoom, precision: 3);

        viewModel.AdjustThumbnailZoom(-99);
        Assert.Equal(0.6, viewModel.ThumbnailZoom, precision: 3);
    }

    private static SourcePdfFile CreateSourceFile(string path, int pageCount)
    {
        var sourceFileId = Guid.NewGuid();
        var pages = Enumerable.Range(1, pageCount)
            .Select(pageNumber => new SourcePdfPage(Guid.NewGuid(), sourceFileId, pageNumber)
            {
                Width = 600,
                Height = 800
            });
        return new SourcePdfFile(sourceFileId, path, Path.GetFileName(path), pageCount, 1024, "fingerprint", pages: pages);
    }

    private static MainViewModel CreateViewModelWith(params SourcePdfFile[] sourceFiles)
    {
        var viewModel = CreateViewModelWithServices();
        foreach (var sourceFile in sourceFiles)
        {
            viewModel.SourceFiles.Add(sourceFile);
        }

        return viewModel;
    }

    private static MainViewModel CreateViewModelWithServices(
        FakePdfMetadataService? metadata = null,
        FakePdfRenderService? render = null,
        FakeFileDialogService? fileDialog = null,
        FakePdfExportService? export = null,
        FakeFoxitLauncherService? foxit = null,
        IAutoSaveService? autoSave = null)
    {
        render ??= new FakePdfRenderService();
        var cache = new ThumbnailCacheService();
        var queue = new ThumbnailRenderQueue(render, cache, maxConcurrency: 2);
        return new MainViewModel(
            fileDialog ?? new FakeFileDialogService([]),
            metadata ?? new FakePdfMetadataService(),
            render,
            export ?? new FakePdfExportService(),
            foxit ?? new FakeFoxitLauncherService(),
            queue,
            cache,
            autoSave ?? new FakeAutoSaveService(),
            new FakeUpdateService());
    }

    private static void SelectPagesAndAddToOutput(MainViewModel viewModel, SourcePdfFile sourceFile, params int[] pageNumbers)
    {
        foreach (var pageNumber in pageNumbers)
        {
            sourceFile.Pages[pageNumber - 1].IsSelected = true;
        }

        viewModel.AddSelectionToOutputCommand.Execute(null);
    }

    private sealed class FakeFileDialogService : IFileDialogService
    {
        private readonly IReadOnlyList<string> paths;
        private readonly string? outputPath;

        public FakeFileDialogService(IReadOnlyList<string> paths, string? outputPath = null)
        {
            this.paths = paths;
            this.outputPath = outputPath;
        }

        public IReadOnlyList<string> PickPdfFiles() => paths;

        public string? PickOutputPdfFile() => outputPath;
    }

    private sealed class FakePdfExportService : IPdfExportService
    {
        public string? OutputPath { get; private set; }

        public int SourceFileCount { get; private set; }

        public int GroupCount { get; private set; }

        public Task ExportAsync(
            IReadOnlyCollection<SourcePdfFile> sourceFiles,
            IReadOnlyCollection<OutputGroup> groups,
            string outputPath,
            IProgress<int>? progress,
            CancellationToken cancellationToken)
        {
            SourceFileCount = sourceFiles.Count;
            GroupCount = groups.Count;
            OutputPath = outputPath;
            var total = groups.Sum(group => group.Items.Count);
            for (var page = 1; page <= total; page++)
            {
                progress?.Report(page);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakeFoxitLauncherService : IFoxitLauncherService
    {
        public string? OpenedPath { get; private set; }

        public Exception? OpenException { get; init; }

        public Task<string?> DiscoverFoxitExecutableAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<string?>(null);
        }

        public bool IsValidExecutable(string? executablePath) => true;

        public Task OpenPdfAsync(string pdfPath, CancellationToken cancellationToken)
        {
            if (OpenException is not null)
            {
                throw OpenException;
            }

            OpenedPath = pdfPath;
            return Task.CompletedTask;
        }
    }

    private sealed class FakePdfMetadataService : IPdfMetadataService
    {
        private readonly Dictionary<string, SourcePdfFile> files = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, PdfMetadataError> errors = new(StringComparer.OrdinalIgnoreCase);

        public void Add(string path, SourcePdfFile sourceFile)
        {
            files[path] = sourceFile;
        }

        public void AddError(string path, PdfMetadataError error)
        {
            errors[path] = error;
        }

        public Task<SourcePdfFile> ReadAsync(string filePath, CancellationToken cancellationToken)
        {
            if (errors.TryGetValue(filePath, out var error))
            {
                throw new PdfMetadataException(filePath, error, "fake error");
            }

            return Task.FromResult(files[filePath]);
        }
    }

    private sealed class FakePdfRenderService : IPdfRenderService
    {
        public int CallCount { get; private set; }

        public List<int> PixelWidths { get; } = [];

        public Exception? Exception { get; init; }

        public TimeSpan Delay { get; init; }

        public async Task<PdfPageRenderResult> RenderPageAsync(
            string filePath,
            int pageNumber,
            int pixelWidth,
            CancellationToken cancellationToken)
        {
            CallCount++;
            PixelWidths.Add(pixelWidth);
            if (Delay > TimeSpan.Zero)
            {
                await Task.Delay(Delay, cancellationToken);
            }

            if (Exception is not null)
            {
                throw Exception;
            }

            return new PdfPageRenderResult(pixelWidth, pixelWidth, pixelWidth * 4, new byte[pixelWidth * pixelWidth * 4]);
        }
    }

    private sealed class FakeAutoSaveService : IAutoSaveService
    {
        private ProjectState? scheduledState;

        public string RecoveryPath => @"D:\Recovery\recovery.ppc.json";

        public bool HasRecovery { get; set; }

        public ProjectState? RecoveryState { get; set; }

        public int ScheduleCount { get; private set; }

        public void Schedule(ProjectState state)
        {
            scheduledState = state;
            ScheduleCount++;
        }

        public Task FlushAsync(CancellationToken cancellationToken)
        {
            RecoveryState = scheduledState;
            scheduledState = null;
            return Task.CompletedTask;
        }

        public Task<ProjectState?> LoadRecoveryAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(RecoveryState);
        }

        public void ClearRecovery()
        {
            HasRecovery = false;
            RecoveryState = null;
        }
    }

    private sealed class FakeUpdateService : IUpdateService
    {
        public AppUpdateResult Result { get; init; } = new(
            AppUpdateStatus.UpToDate,
            "Dang dung phien ban moi nhat");

        public Task<AppUpdateResult> CheckAndInstallUpdateAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(Result);
        }
    }
}
