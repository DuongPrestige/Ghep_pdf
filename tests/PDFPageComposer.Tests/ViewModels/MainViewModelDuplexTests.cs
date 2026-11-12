using PDFPageComposer.App.Models;
using PDFPageComposer.App.Services;
using PDFPageComposer.App.ViewModels;

namespace PDFPageComposer.Tests.ViewModels;

/// <summary>"Ghép 2 mặt" mode (TASK-083).</summary>
public sealed partial class MainViewModelTests
{
    [Fact]
    public async Task Duplex_mode_adds_side_1_then_side_2_and_exports_all_side_1_then_all_side_2()
    {
        var congb = CreateSourceFile(@"D:\Card\CONGB.pdf", 3, "congb");
        var hung = CreateSourceFile(@"D:\Card\QUANG HUNG.pdf", 4, "hung");
        var signatures = CreateSourceFile(@"D:\Card\chu ky.pdf", 2, "sig");
        var export = new FakePdfExportService();
        var foxit = new FakeFoxitLauncherService();
        var viewModel = CreateViewModelWithServices(
            fileDialog: new FakeFileDialogService([], @"D:\Out\don 12.11.pdf"),
            export: export,
            foxit: foxit);
        foreach (var file in new[] { congb, hung, signatures })
        {
            viewModel.SourceFiles.Add(file);
        }

        viewModel.IsDuplexMode = true;
        Assert.Equal("Thêm mặt 1", viewModel.AddSelectionButtonText);

        SelectPagesAndAddToOutput(viewModel, congb, 1, 2, 3);
        Assert.Equal("Thêm mặt 2", viewModel.AddSelectionButtonText);
        Assert.True(viewModel.HasDuplexProblem);
        SelectPagesAndAddToOutput(viewModel, signatures, 1);
        Assert.Equal("Thêm mặt 1", viewModel.AddSelectionButtonText);

        SelectPagesAndAddToOutput(viewModel, hung, 1, 2, 3, 4);
        SelectPagesAndAddToOutput(viewModel, signatures, 2);

        Assert.Empty(viewModel.OutputGroups);
        Assert.Equal(2, viewModel.DuplexPairs.Count);
        Assert.False(viewModel.HasDuplexProblem);
        Assert.Equal(7, viewModel.DuplexSheetCount);
        Assert.Equal(0, viewModel.SelectedPageCount);
        Assert.Equal(3, signatures.Pages[0].OutputOccurrenceCount);
        Assert.Equal(4, signatures.Pages[1].OutputOccurrenceCount);

        var rows = viewModel.DuplexRows;
        Assert.Equal("Cặp 1 · CONGB.pdf", rows[0].Header);
        Assert.Equal([4, 5, 6, 7], rows[1].FrontPages.Select(page => page.Position));
        Assert.Equal([4, 5, 6, 7], rows[1].BackPages.Select(page => page.Position));

        await viewModel.ExportPdfCommand.ExecuteAsync(null);

        var exported = Assert.Single(export.Exports);
        Assert.Equal(@"D:\Out\don 12.11.pdf", exported.Path);
        Assert.Equal(
            [(congb.Id, 1), (congb.Id, 2), (congb.Id, 3), (hung.Id, 1), (hung.Id, 2), (hung.Id, 3), (hung.Id, 4),
             .. Enumerable.Repeat((signatures.Id, 1), 3), .. Enumerable.Repeat((signatures.Id, 2), 4)],
            exported.Pages);
        Assert.Equal([@"D:\Out\don 12.11.pdf"], foxit.OpenedPaths);
        Assert.Equal("Đã xuất PDF: don 12.11.pdf (mặt 1: trang 1–7, mặt 2: trang 8–14)", viewModel.RenderStatus);
    }

    [Fact]
    public async Task Duplex_export_is_blocked_when_a_pair_has_different_page_counts()
    {
        var photos = CreateSourceFile(@"D:\Card\PMC.pdf", 3, "pmc");
        var signatures = CreateSourceFile(@"D:\Card\chu ky.pdf", 2, "sig");
        var export = new FakePdfExportService();
        var viewModel = CreateViewModelWithServices(
            fileDialog: new FakeFileDialogService([], @"D:\Out\don.pdf"),
            export: export);
        viewModel.SourceFiles.Add(photos);
        viewModel.SourceFiles.Add(signatures);
        viewModel.IsDuplexMode = true;

        SelectPagesAndAddToOutput(viewModel, photos, 1, 2, 3);
        SelectPagesAndAddToOutput(viewModel, signatures, 1, 2);

        Assert.Equal("Lệch: mặt 1 có 3 trang, mặt 2 có 2 trang", viewModel.DuplexRows[0].StatusText);
        Assert.Equal("Chưa xuất được: lệch số trang: cặp 1", viewModel.DuplexProblemText);
        Assert.Equal("Thêm mặt 1", viewModel.AddSelectionButtonText);

        await viewModel.ExportPdfCommand.ExecuteAsync(null);

        Assert.Empty(export.Exports);
        Assert.Equal("Chưa xuất được: lệch số trang: cặp 1", viewModel.RenderStatus);

        // "Thay" on side 2 with one page repeats it, which fixes the pair.
        signatures.Pages[0].IsSelected = true;
        viewModel.ReplaceDuplexBackCommand.Execute(viewModel.DuplexRows[0]);

        Assert.True(viewModel.DuplexPairs[0].IsComplete);
        Assert.False(viewModel.HasDuplexProblem);
    }

    [Fact]
    public void Thay_with_nothing_selected_waits_for_the_next_add_and_can_be_cancelled()
    {
        var first = CreateSourceFile(@"D:\Card\CONGB 1.pdf", 1, "c1");
        var second = CreateSourceFile(@"D:\Card\CONGB 2.pdf", 2, "c2");
        var signatures = CreateSourceFile(@"D:\Card\chu ky.pdf", 1, "sig");
        var viewModel = CreateViewModelWith(first, second, signatures);
        viewModel.IsDuplexMode = true;
        SelectPagesAndAddToOutput(viewModel, first, 1);
        SelectPagesAndAddToOutput(viewModel, signatures, 1);

        viewModel.ReplaceDuplexFrontCommand.Execute(viewModel.DuplexRows[0]);

        Assert.Equal("Thay mặt 1 · cặp 1", viewModel.AddSelectionButtonText);
        Assert.True(viewModel.DuplexRows[0].IsReplacingFront);
        Assert.Equal("Hủy thay", viewModel.DuplexRows[0].FrontReplaceText);

        // Pressing it again cancels.
        viewModel.ReplaceDuplexFrontCommand.Execute(viewModel.DuplexRows[0]);
        Assert.Equal("Thêm mặt 1", viewModel.AddSelectionButtonText);
        Assert.False(viewModel.DuplexRows[0].IsReplacingFront);

        viewModel.ReplaceDuplexFrontCommand.Execute(viewModel.DuplexRows[0]);
        SelectPagesAndAddToOutput(viewModel, second, 1, 2);

        var pair = Assert.Single(viewModel.DuplexPairs);
        Assert.Equal([(second.Id, 1), (second.Id, 2)], pair.FrontItems.Select(item => (item.SourceFileId, item.SourcePageNumber)));
        Assert.Equal("Lệch: mặt 1 có 2 trang, mặt 2 có 1 trang", viewModel.DuplexRows[0].StatusText);
        Assert.Equal("Thêm mặt 1", viewModel.AddSelectionButtonText);

        // Side 2: arm, then a single signature page is repeated to match side 1.
        viewModel.ReplaceDuplexBackCommand.Execute(viewModel.DuplexRows[0]);
        SelectPagesAndAddToOutput(viewModel, signatures, 1);
        Assert.True(viewModel.DuplexPairs[0].IsComplete);
        Assert.Single(viewModel.DuplexPairs);
    }

    [Fact]
    public void Them_appends_pages_to_a_side_and_keeps_the_existing_ones()
    {
        var photos = CreateSourceFile(@"D:\Card\CONGB.pdf", 3, "c");
        var signatures = CreateSourceFile(@"D:\Card\chu ky.pdf", 1, "sig");
        var viewModel = CreateViewModelWith(photos, signatures);
        viewModel.IsDuplexMode = true;
        SelectPagesAndAddToOutput(viewModel, photos, 1, 2);
        SelectPagesAndAddToOutput(viewModel, signatures, 1);
        var pair = viewModel.DuplexPairs[0];

        // Pages selected: "Thêm" appends at once.
        photos.Pages[2].IsSelected = true;
        viewModel.AppendDuplexFrontCommand.Execute(viewModel.DuplexRows[0]);
        Assert.Equal([1, 2, 3], pair.FrontItems.Select(item => item.SourcePageNumber));
        Assert.Equal("Lệch: mặt 1 có 3 trang, mặt 2 có 2 trang", viewModel.DuplexRows[0].StatusText);

        // Nothing selected: "Thêm" waits, then the next add appends (one page, not repeated).
        viewModel.AppendDuplexBackCommand.Execute(viewModel.DuplexRows[0]);
        Assert.Equal("Thêm vào mặt 2 · cặp 1", viewModel.AddSelectionButtonText);
        Assert.Equal("Hủy thêm", viewModel.DuplexRows[0].BackAppendText);
        Assert.Equal("Thay", viewModel.DuplexRows[0].BackReplaceText);
        SelectPagesAndAddToOutput(viewModel, signatures, 1);

        Assert.Equal(3, pair.BackItems.Count);
        Assert.True(pair.IsComplete);
        Assert.Single(viewModel.DuplexPairs);
        Assert.Equal("Thêm mặt 1", viewModel.AddSelectionButtonText);

        viewModel.UndoCommand.Execute(null);
        Assert.Equal(2, viewModel.DuplexPairs[0].BackItems.Count);
    }

    [Fact]
    public void Duplex_pairs_can_be_duplicated_moved_deleted_and_undone()
    {
        var photos = CreateSourceFile(@"D:\Card\A.pdf", 2, "a");
        var other = CreateSourceFile(@"D:\Card\B.pdf", 1, "b");
        var signatures = CreateSourceFile(@"D:\Card\chu ky.pdf", 1, "sig");
        var viewModel = CreateViewModelWith(photos, other, signatures);
        viewModel.IsDuplexMode = true;
        SelectPagesAndAddToOutput(viewModel, photos, 1, 2);
        SelectPagesAndAddToOutput(viewModel, signatures, 1);
        SelectPagesAndAddToOutput(viewModel, other, 1);
        SelectPagesAndAddToOutput(viewModel, signatures, 1);
        var first = viewModel.DuplexPairs[0];
        var second = viewModel.DuplexPairs[1];

        viewModel.MoveDuplexPairDownCommand.Execute(viewModel.DuplexRows[0]);
        Assert.Equal([second, first], viewModel.DuplexPairs);

        viewModel.DuplicateDuplexPairCommand.Execute(viewModel.DuplexRows[1]);
        Assert.Equal(3, viewModel.DuplexPairs.Count);
        Assert.Equal(
            first.FrontItems.Select(item => item.SourcePageNumber),
            viewModel.DuplexPairs[2].FrontItems.Select(item => item.SourcePageNumber));
        Assert.Equal(2, viewModel.DuplexPairs[2].BackItems.Count);
        Assert.NotEqual(first.Id, viewModel.DuplexPairs[2].Id);

        viewModel.DeleteDuplexPairCommand.Execute(viewModel.DuplexRows[0]);
        Assert.Equal(2, viewModel.DuplexPairs.Count);

        viewModel.UndoCommand.Execute(null);
        viewModel.UndoCommand.Execute(null);
        Assert.Equal(2, viewModel.DuplexPairs.Count);
        Assert.Equal(second.Id, viewModel.DuplexPairs[0].Id);
        Assert.Equal(first.Id, viewModel.DuplexPairs[1].Id);
    }

    [Fact]
    public void Duplex_mode_keeps_the_normal_tray_and_counts_only_its_own_pages()
    {
        var photos = CreateSourceFile(@"D:\Card\A.pdf", 2, "a");
        var viewModel = CreateViewModelWith(photos);
        SelectPagesAndAddToOutput(viewModel, photos, 1);
        Assert.Equal(1, photos.Pages[0].OutputOccurrenceCount);

        viewModel.IsDuplexMode = true;
        Assert.Equal(0, photos.Pages[0].OutputOccurrenceCount);
        Assert.False(viewModel.PreviewOutputCommand.CanExecute(null));
        Assert.Equal("Xuất PDF", viewModel.ExportButtonText);
        SelectPagesAndAddToOutput(viewModel, photos, 2);

        viewModel.IsDuplexMode = false;
        Assert.Single(viewModel.OutputGroups);
        Assert.Single(viewModel.DuplexPairs);
        Assert.Equal("Thêm vào đầu ra", viewModel.AddSelectionButtonText);
        Assert.Equal(1, photos.Pages[0].OutputOccurrenceCount);
        Assert.Equal(0, photos.Pages[1].OutputOccurrenceCount);

        viewModel.RemoveSourceFileCommand.Execute(photos);
        Assert.Single(viewModel.SourceFiles);
    }

    [Fact]
    public void Duplex_pairs_survive_project_save_and_load()
    {
        var photos = CreateSourceFile(@"D:\Card\A.pdf", 2, "a");
        var signatures = CreateSourceFile(@"D:\Card\chu ky.pdf", 1, "sig");
        var viewModel = CreateViewModelWith(photos, signatures);
        viewModel.IsDuplexMode = true;
        SelectPagesAndAddToOutput(viewModel, photos, 1, 2);
        SelectPagesAndAddToOutput(viewModel, signatures, 1);

        var state = ProjectStateMapper.FromSession(
            viewModel.SourceFiles,
            viewModel.OutputGroups,
            1.0,
            duplexPairs: viewModel.DuplexPairs,
            isDuplexMode: viewModel.IsDuplexMode);
        var json = System.Text.Json.JsonSerializer.Serialize(state);
        var restored = ProjectStateMapper.ToSession(System.Text.Json.JsonSerializer.Deserialize<ProjectState>(json)!);

        Assert.True(restored.IsDuplexMode);
        var pair = Assert.Single(restored.DuplexPairs!);
        Assert.Equal(viewModel.DuplexPairs[0].Id, pair.Id);
        Assert.Equal([1, 2], pair.FrontItems.Select(item => item.SourcePageNumber));
        Assert.Equal([1, 1], pair.BackItems.Select(item => item.SourcePageNumber));
        Assert.All(pair.AllItems, item => Assert.Equal(pair.Id, item.GroupId));

        var oldProject = ProjectStateMapper.ToSession(
            System.Text.Json.JsonSerializer.Deserialize<ProjectState>("""{"Version":1,"SourceFiles":[],"OutputGroups":[]}""")!);
        Assert.Empty(oldProject.DuplexPairs!);
        Assert.False(oldProject.IsDuplexMode);
    }
}
