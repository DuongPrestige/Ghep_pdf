using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PDFPageComposer.App.Models;
using PDFPageComposer.App.Services;

namespace PDFPageComposer.App.ViewModels;

/// <summary>
/// "Ghép 2 mặt" mode: each order is a pair (side 1 + side 2) added in turn with "Thêm mặt 1" / "Thêm mặt 2";
/// export writes one PDF: side 1 of every pair, then side 2 of every pair in the same order. The pairs are kept
/// apart from the normal output tray.
/// </summary>
public partial class MainViewModel
{
    [ObservableProperty]
    private bool isDuplexMode;

    /// <summary>Side armed by "Thay" / "Thêm" with nothing selected; the next add replaces or extends it.</summary>
    private (Guid PairId, DuplexSide Side, DuplexEditMode Mode)? duplexEditTarget;

    public ObservableCollection<DuplexPair> DuplexPairs { get; } = [];

    /// <summary>The last pair has side 1 but no side 2 yet, so the next add fills its side 2.</summary>
    public bool IsAwaitingDuplexBack => DuplexPairs.Count > 0 && !DuplexPairs[^1].HasBack;

    public string AddSelectionButtonText => !IsDuplexMode
        ? "Thêm vào đầu ra"
        : GetDuplexEditTarget() is { } edit
            ? edit.Mode == DuplexEditMode.Replace
                ? $"Thay mặt {(int)edit.Side} · cặp {DuplexPairs.IndexOf(edit.Pair) + 1}"
                : $"Thêm vào mặt {(int)edit.Side} · cặp {DuplexPairs.IndexOf(edit.Pair) + 1}"
            : IsAwaitingDuplexBack ? "Thêm mặt 2" : "Thêm mặt 1";

    public string AddSelectionToolTip => !IsDuplexMode
        ? "Thêm các trang đang chọn vào khay đầu ra (phím Enter)"
        : GetDuplexEditTarget() is { } armed
            ? armed.Mode == DuplexEditMode.Replace
                ? "Thay mặt đã chọn bằng các trang đang chọn (phím Enter). Bấm \"Hủy thay\" ở khay để thôi."
                : "Thêm các trang đang chọn vào cuối mặt đã chọn (phím Enter). Bấm \"Hủy thêm\" ở khay để thôi."
        : IsAwaitingDuplexBack
            ? $"Thêm các trang đang chọn làm mặt 2 của cặp {DuplexPairs.Count} (phím Enter). Chọn 1 trang thì trang đó được lặp cho đủ số trang mặt 1."
            : "Thêm các trang đang chọn làm mặt 1 của một cặp mới (phím Enter)";

    public string ExportButtonText => "Xuất PDF";

    public int DuplexSheetCount => DuplexPairs.Sum(pair => pair.FrontItems.Count);

    public string DuplexSummary => $"{DuplexPairs.Count} cặp · mặt 1: {DuplexSheetCount} trang · mặt 2: {DuplexPairs.Sum(pair => pair.BackItems.Count)} trang";

    /// <summary>Why the duplex files cannot be exported, or empty when they can.</summary>
    public string DuplexProblemText => DuplexPairs.Count == 0
        ? string.Empty
        : DuplexLayout.DescribeProblem(DuplexPairs) ?? string.Empty;

    public bool HasDuplexProblem => DuplexProblemText.Length > 0;

    public string OutputStatusText => IsDuplexMode
        ? $"{DuplexPairs.Count} cặp, {DuplexSheetCount} tờ"
        : OutputPageCount.ToString();

    public bool CanPreviewOutput => HasOutputPages && !IsDuplexMode;

    public IReadOnlyList<DuplexPairRowView> DuplexRows
    {
        get
        {
            var rows = new List<DuplexPairRowView>(DuplexPairs.Count);
            var frontOffset = 0;
            var backOffset = 0;
            for (var index = 0; index < DuplexPairs.Count; index++)
            {
                var pair = DuplexPairs[index];
                var frontPages = ToPageViews(pair.FrontItems, frontOffset);
                var backPages = ToPageViews(pair.BackItems, backOffset);
                frontOffset += frontPages.Count;
                backOffset += backPages.Count;
                rows.Add(new DuplexPairRowView(
                    index + 1,
                    frontPages.FirstOrDefault()?.SourceDisplayName ?? "Chưa có mặt 1",
                    pair,
                    frontPages,
                    backPages,
                    DuplexLayout.DescribePair(pair),
                    pair.IsComplete,
                    duplexEditTarget is { } target && target.PairId == pair.Id ? target.Side : null,
                    duplexEditTarget?.Mode ?? DuplexEditMode.Replace));
            }

            return rows;
        }
    }

    [RelayCommand]
    private void ReplaceDuplexFront(DuplexPairRowView? row)
    {
        EditDuplexSide(row, DuplexSide.Front, DuplexEditMode.Replace);
    }

    [RelayCommand]
    private void ReplaceDuplexBack(DuplexPairRowView? row)
    {
        EditDuplexSide(row, DuplexSide.Back, DuplexEditMode.Replace);
    }

    [RelayCommand]
    private void AppendDuplexFront(DuplexPairRowView? row)
    {
        EditDuplexSide(row, DuplexSide.Front, DuplexEditMode.Append);
    }

    [RelayCommand]
    private void AppendDuplexBack(DuplexPairRowView? row)
    {
        EditDuplexSide(row, DuplexSide.Back, DuplexEditMode.Append);
    }

    [RelayCommand]
    private void DeleteDuplexPair(DuplexPairRowView? row)
    {
        if (row is null || !DuplexPairs.Contains(row.Pair))
        {
            return;
        }

        CaptureUndoSnapshot();
        DuplexPairs.Remove(row.Pair);
        RecalculateOutputOccurrences();
        NotifyStatisticsChanged();
        RenderStatus = $"Đã xóa cặp {row.Number}";
    }

    [RelayCommand]
    private void DuplicateDuplexPair(DuplexPairRowView? row)
    {
        if (row is null)
        {
            return;
        }

        var index = DuplexPairs.IndexOf(row.Pair);
        if (index < 0)
        {
            return;
        }

        CaptureUndoSnapshot();
        var pairId = Guid.NewGuid();
        DuplexPairs.Insert(index + 1, new DuplexPair(
            pairId,
            GetNextDuplexColor(),
            row.Pair.FrontItems.Select(item => CopyItem(item, pairId)),
            row.Pair.BackItems.Select(item => CopyItem(item, pairId))));
        RecalculateOutputOccurrences();
        NotifyStatisticsChanged();
        RenderStatus = $"Đã nhân bản cặp {row.Number}";
    }

    [RelayCommand]
    private void MoveDuplexPairUp(DuplexPairRowView? row)
    {
        MoveDuplexPair(row, -1);
    }

    [RelayCommand]
    private void MoveDuplexPairDown(DuplexPairRowView? row)
    {
        MoveDuplexPair(row, 1);
    }

    partial void OnIsDuplexModeChanged(bool value)
    {
        duplexEditTarget = null;
        RecalculateOutputOccurrences();
        NotifyStatisticsChanged();
        RenderStatus = value
            ? "Chế độ ghép 2 mặt: chọn trang rồi bấm Thêm mặt 1, sau đó Thêm mặt 2"
            : "Chế độ ghép lẻ";
    }

    /// <summary>
    /// "Thêm mặt 1" / "Thêm mặt 2": fills side 2 of the last pair when it is still missing, otherwise starts a new
    /// pair with the selection as side 1.
    /// </summary>
    private void AddSelectionToDuplex(IReadOnlyList<SourcePdfPage> selectedPages)
    {
        if (GetDuplexEditTarget() is { } edit)
        {
            ApplyDuplexEdit(edit.Pair, edit.Side, edit.Mode, selectedPages);
            return;
        }

        CaptureUndoSnapshot();
        if (IsAwaitingDuplexBack)
        {
            var pair = DuplexPairs[^1];
            foreach (var item in DuplexLayout.CreateBackItems(pair.Id, selectedPages, pair.FrontItems.Count))
            {
                pair.BackItems.Add(item);
            }

            RenderStatus = pair.IsComplete
                ? $"Cặp {DuplexPairs.Count}: đã thêm mặt 2 ({pair.BackItems.Count} trang)"
                : $"Cặp {DuplexPairs.Count}: {DuplexLayout.DescribePair(pair)}";
        }
        else
        {
            var pairId = Guid.NewGuid();
            DuplexPairs.Add(new DuplexPair(pairId, GetNextDuplexColor(), DuplexLayout.CreateItems(pairId, selectedPages)));
            RenderStatus = $"Cặp {DuplexPairs.Count}: đã thêm mặt 1 ({selectedPages.Count} trang), chọn trang mặt 2";
        }

        FinishAddingSelection();
    }

    /// <summary>
    /// "Thay" / "Thêm" of a side: with pages selected, replaces or extends that side at once. With nothing
    /// selected, waits for the next add ("Thay mặt X · cặp N" / "Thêm vào mặt X · cặp N" / Enter); pressing
    /// the same button again ("Hủy thay" / "Hủy thêm") cancels.
    /// </summary>
    private void EditDuplexSide(DuplexPairRowView? row, DuplexSide side, DuplexEditMode mode)
    {
        if (row is null || !DuplexPairs.Contains(row.Pair))
        {
            return;
        }

        var selectedPages = GetSelectedPagesInOrder();
        if (selectedPages.Count == 0)
        {
            var verb = mode == DuplexEditMode.Replace ? "thay" : "thêm vào";
            if (duplexEditTarget == (row.Pair.Id, side, mode))
            {
                duplexEditTarget = null;
                RenderStatus = $"Đã hủy {verb} mặt {(int)side} của cặp {row.Number}";
            }
            else
            {
                duplexEditTarget = (row.Pair.Id, side, mode);
                RenderStatus = $"Chọn trang rồi bấm \"{AddSelectionButtonText}\" (hoặc Enter)";
            }

            NotifyDuplexChanged();
            return;
        }

        ApplyDuplexEdit(row.Pair, side, mode, selectedPages);
    }

    private void ApplyDuplexEdit(DuplexPair pair, DuplexSide side, DuplexEditMode mode, IReadOnlyList<SourcePdfPage> selectedPages)
    {
        CaptureUndoSnapshot();
        duplexEditTarget = null;
        var target = side == DuplexSide.Front ? pair.FrontItems : pair.BackItems;

        // Side 2 that is empty or being replaced follows the "Thêm mặt 2" rule: one page is repeated to match side 1.
        var fillsWholeSide = mode == DuplexEditMode.Replace || target.Count == 0;
        var items = side == DuplexSide.Back && fillsWholeSide
            ? DuplexLayout.CreateBackItems(pair.Id, selectedPages, pair.FrontItems.Count)
            : DuplexLayout.CreateItems(pair.Id, selectedPages);
        if (mode == DuplexEditMode.Replace)
        {
            target.Clear();
        }

        foreach (var item in items)
        {
            target.Add(item);
        }

        FinishAddingSelection();
        var done = mode == DuplexEditMode.Replace ? "đã thay" : "đã thêm";
        RenderStatus = $"Cặp {DuplexPairs.IndexOf(pair) + 1}: {done} {items.Count} trang mặt {(int)side} · {DuplexLayout.DescribePair(pair)}";
    }

    /// <summary>The side waiting for the next add, when its pair still exists.</summary>
    private (DuplexPair Pair, DuplexSide Side, DuplexEditMode Mode)? GetDuplexEditTarget()
    {
        if (duplexEditTarget is not { } target)
        {
            return null;
        }

        var pair = DuplexPairs.FirstOrDefault(candidate => candidate.Id == target.PairId);
        return pair is null ? null : (pair, target.Side, target.Mode);
    }

    private void MoveDuplexPair(DuplexPairRowView? row, int delta)
    {
        if (row is null)
        {
            return;
        }

        var index = DuplexPairs.IndexOf(row.Pair);
        var newIndex = index + delta;
        if (index < 0 || newIndex < 0 || newIndex >= DuplexPairs.Count)
        {
            return;
        }

        CaptureUndoSnapshot();
        DuplexPairs.Move(index, newIndex);
        NotifyStatisticsChanged();
        RenderStatus = $"Đã chuyển cặp {row.Number} thành cặp {newIndex + 1}";
    }

    private async Task ExportDuplexAsync(CancellationToken cancellationToken)
    {
        var problem = DuplexLayout.DescribeProblem(DuplexPairs);
        if (problem is not null)
        {
            RenderStatus = problem;
            return;
        }

        var chosenPath = fileDialogService.PickOutputPdfFile();
        if (string.IsNullOrWhiteSpace(chosenPath))
        {
            RenderStatus = "Chưa chọn nơi lưu PDF";
            return;
        }

        var sheetCount = DuplexSheetCount;
        try
        {
            ExportProgressPageCount = 0;
            ExportProgressTotalPageCount = sheetCount * 2;
            ExportProgressPercent = 0;
            IsExporting = true;

            await pdfExportService.ExportAsync(
                SourceFiles,
                DuplexLayout.ToExportGroups(DuplexPairs),
                chosenPath,
                CreateExportProgress(0),
                cancellationToken);

            ExportProgressPercent = 100;
            RenderStatus = $"Đã xuất PDF: {Path.GetFileName(chosenPath)} (mặt 1: trang 1–{sheetCount}, mặt 2: trang {sheetCount + 1}–{sheetCount * 2})";
            await OpenExportedPdfAsync(chosenPath, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            RenderStatus = "Đã hủy xuất PDF";
        }
        catch (PdfExportException ex)
        {
            RenderStatus = DescribeExportError(ex.Error);
        }
        finally
        {
            IsExporting = false;
        }
    }

    private ExportProgress CreateExportProgress(int pageOffset)
    {
        return new ExportProgress(pageCount =>
        {
            var done = pageOffset + pageCount;
            ExportProgressPageCount = done;
            ExportProgressPercent = ExportProgressTotalPageCount == 0
                ? 0
                : Math.Clamp((int)Math.Round(done * 100.0 / ExportProgressTotalPageCount), 0, 100);
            RenderStatus = $"Đang xuất {done}/{ExportProgressTotalPageCount} trang";
        });
    }

    private async Task OpenExportedPdfAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            await foxitLauncherService.OpenPdfAsync(path, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            RenderStatus = $"Đã xuất PDF nhưng không mở được file: {Path.GetFileName(path)}";
        }
    }

    private List<DuplexPageView> ToPageViews(IEnumerable<OutputPageItem> items, int offset)
    {
        return items
            .Select((item, index) =>
            {
                var sourceFile = SourceFiles.FirstOrDefault(file => file.Id == item.SourceFileId);
                return new DuplexPageView(
                    offset + index + 1,
                    sourceFile?.DisplayName ?? "Thiếu file nguồn",
                    item.SourcePageNumber,
                    sourceFile?.Pages.FirstOrDefault(page => page.PageNumber == item.SourcePageNumber));
            })
            .ToList();
    }

    private string GetNextDuplexColor()
    {
        // Neighbouring pairs get different colours so their pages are easy to tell apart.
        var previous = DuplexPairs.LastOrDefault()?.ColorHex;
        var index = previous is null ? 0 : (Array.IndexOf(OutputGroupColors, previous) + 1) % OutputGroupColors.Length;
        return OutputGroupColors[Math.Max(0, index)];
    }

    private static OutputPageItem CopyItem(OutputPageItem item, Guid groupId)
    {
        return new OutputPageItem(Guid.NewGuid(), groupId, item.SourceFileId, item.SourcePageNumber);
    }

    private void NotifyDuplexChanged()
    {
        OnPropertyChanged(nameof(DuplexRows));
        OnPropertyChanged(nameof(IsAwaitingDuplexBack));
        OnPropertyChanged(nameof(AddSelectionButtonText));
        OnPropertyChanged(nameof(AddSelectionToolTip));
        OnPropertyChanged(nameof(ExportButtonText));
        OnPropertyChanged(nameof(DuplexSheetCount));
        OnPropertyChanged(nameof(DuplexSummary));
        OnPropertyChanged(nameof(DuplexProblemText));
        OnPropertyChanged(nameof(HasDuplexProblem));
        OnPropertyChanged(nameof(OutputStatusText));
        OnPropertyChanged(nameof(CanPreviewOutput));
    }

    private sealed record DuplexPairSnapshot(
        Guid Id,
        string ColorHex,
        IReadOnlyList<OutputPageItem> FrontItems,
        IReadOnlyList<OutputPageItem> BackItems);
}
