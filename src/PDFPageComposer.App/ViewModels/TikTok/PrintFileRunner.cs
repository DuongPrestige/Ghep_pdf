using System.Globalization;
using System.IO;
using PDFPageComposer.App.Models;
using PDFPageComposer.App.TikTok.Models;
using Serilog;

namespace PDFPageComposer.App.ViewModels.TikTok;

/// <summary>Result of one "Tạo file in" run. FoxitWarning is set when the file was created but could not be opened.</summary>
public sealed record PrintRunOutcome(PrintBatchResult Result, string? FoxitWarning);

/// <summary>
/// Shared export flow of the Orders and History tabs: choose the output path, export the plan with progress,
/// then open the file in Foxit. Nothing is marked printed unless the exporter succeeds.
/// </summary>
public sealed class PrintFileRunner
{
    private readonly TikTokWorkspace workspace;

    public PrintFileRunner(TikTokWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        this.workspace = workspace;
    }

    public static string DescribePlan(PrintPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var duplicates = plan.Orders.SelectMany(order => order.Copies).Count(copy => copy.DuplicateImage);
        var separators = plan.Orders.Count(order => order.IncludeSeparator);
        var text = $"{plan.Orders.Count} đơn, {plan.TotalPages} trang ({separators} trang phân cách)";
        if (duplicates > 0)
        {
            text += $", {duplicates} bộ trùng hình";
        }

        if (plan.Rejected.Count > 0)
        {
            text += $"; {plan.Rejected.Count} đơn bị loại";
        }

        return text;
    }

    public string SuggestFileName(int orderCount) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"TIKTOK_{workspace.Now:yyyyMMdd_HHmm}_{orderCount}don.pdf");

    /// <summary>Vietnamese text for an export failure (AGENTS.md: UI text is Vietnamese).</summary>
    public static string DescribeExportError(PdfExportError error, string outputPath) =>
        error switch
        {
            PdfExportError.DestinationUnavailable =>
                $"Không ghi được file in vào '{Path.GetDirectoryName(outputPath)}'. Kiểm tra thư mục lưu file in (ổ đĩa đã cắm/kết nối, quyền ghi, file không bị mở ở ứng dụng khác).",
            PdfExportError.OutputMatchesSource => "Không được ghi đè lên file PDF nguồn trong kho. Hãy chọn tên hoặc thư mục khác.",
            PdfExportError.MissingSource => "Thiếu file PDF nguồn trong kho (đã bị xóa hoặc di chuyển). Hãy quét lại kho rồi thử lại.",
            PdfExportError.SourceChanged => "File PDF nguồn đã thay đổi kể từ lần quét. Hãy quét lại kho rồi thử lại.",
            PdfExportError.InvalidPage => "Trang trong file PDF nguồn không hợp lệ. Hãy quét lại kho.",
            PdfExportError.EmptyOutput => "Không có trang nào để tạo file in.",
            _ => "Không tạo được file in.",
        };

    /// <summary>Returns null when the user cancelled the save dialog.</summary>
    public async Task<PrintRunOutcome?> ExportAsync(PrintPlan plan, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.Orders.Count == 0)
        {
            throw new InvalidOperationException("Không có đơn nào đủ điều kiện để tạo file in.");
        }

        var outputPath = await ResolveOutputPathAsync(plan.Orders.Count, cancellationToken);
        if (outputPath is null)
        {
            return null;
        }

        var totalPages = plan.TotalPages;
        var progress = new Progress<int>(done =>
        {
            if (workspace.IsBusy)
            {
                workspace.ProgressText = $"Đang ghép trang {Math.Min(done, totalPages)}/{totalPages}";
            }
        });

        var services = workspace.Services;
        PrintBatchResult result;
        try
        {
            result = await services.Exporter.ExportAsync(
                plan,
                workspace.ShopNames(),
                outputPath,
                progress,
                cancellationToken);
        }
        catch (PdfExportException exception)
        {
            throw new InvalidOperationException(DescribeExportError(exception.Error, outputPath), exception);
        }

        string? foxitWarning = null;
        try
        {
            // The batch is already committed, so a late cancel must not skip opening the file.
            await services.FoxitLauncher.OpenPdfAsync(result.OutputPath, CancellationToken.None);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Log.Warning("Opening the TikTok print file failed with {ExceptionType}", exception.GetType().FullName);
            foxitWarning = $"Đã tạo file nhưng không mở được bằng Foxit: {exception.Message}";
        }

        return new PrintRunOutcome(result, foxitWarning);
    }

    /// <summary>
    /// Never returns a path that already holds a file or the file of an earlier batch: an auto-named file in the
    /// output folder gets a "_2", "_3"... suffix, so a batch in the same minute cannot replace an earlier one (TT-11).
    /// </summary>
    private async Task<string?> ResolveOutputPathAsync(int orderCount, CancellationToken cancellationToken)
    {
        var fileName = SuggestFileName(orderCount);
        var folder = workspace.Settings.OutputFolder;
        var repository = workspace.Services.Repository;
        var batches = await Task.Run(() => repository.GetPrintBatchesAsync(cancellationToken), cancellationToken);
        var usedPaths = batches
            .Select(batch => NormalizePath(batch.OutputPath))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(folder))
        {
            // The save dialog already asks before overwriting an existing file.
            var picked = workspace.Services.FileDialogs.PickOutputPdfFile(fileName, null);
            if (picked is not null && usedPaths.Contains(NormalizePath(picked)))
            {
                throw new InvalidOperationException(
                    $"File {Path.GetFileName(picked)} đang là file in của một lô cũ trong Lịch sử. Hãy chọn tên khác để không ghi đè lô đó.");
            }

            return picked;
        }

        var stem = Path.GetFileNameWithoutExtension(fileName);
        var candidate = Path.Combine(folder, fileName);
        for (var suffix = 2; File.Exists(candidate) || usedPaths.Contains(NormalizePath(candidate)); suffix++)
        {
            candidate = Path.Combine(folder, string.Create(CultureInfo.InvariantCulture, $"{stem}_{suffix}.pdf"));
        }

        return candidate;
    }

    private static string NormalizePath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // An unusual stored path still compares as written.
            return path;
        }
    }
}
