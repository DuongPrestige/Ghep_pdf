using System.Globalization;
using System.IO;
using PDFPageComposer.App.TikTok.Export;

namespace PDFPageComposer.App.ViewModels.TikTok;

/// <summary>
/// Shared "Xuất danh sách CSV" flow of the Orders and History tabs (PRD_TIKTOK TT-12): build the rows with the
/// current classification settings, ask where to save, write the CSV and report in the status line.
/// </summary>
public sealed class OrderListExportRunner
{
    public const string EmptyText = "Không có đơn nào để xuất.";
    public const string CancelledText = "Đã hủy xuất danh sách đơn.";

    private readonly TikTokWorkspace workspace;

    public OrderListExportRunner(TikTokWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        this.workspace = workspace;
    }

    public static IReadOnlyList<TikTokOption<OrderListExportScope>> ScopeOptions { get; } =
    [
        new(OrderListExportScope.PrintedOnly, "Đơn đã làm file in"),
        new(OrderListExportScope.NotPrinted, "Đơn chưa làm file in"),
        new(OrderListExportScope.AllImported, "Tất cả đơn đang chờ (đánh dấu đã làm)"),
        new(OrderListExportScope.LatestImport, "Lần import gần nhất"),
    ];

    public string SuggestFileName(OrderListExportScope scope, string? batchId)
    {
        var part = scope switch
        {
            OrderListExportScope.PrintedOnly => "DA_IN",
            OrderListExportScope.AllImported => "TAT_CA",
            OrderListExportScope.NotPrinted => "CHUA_IN",
            OrderListExportScope.LatestImport => "IMPORT_MOI",
            OrderListExportScope.PrintBatch => "LO_" + SafeFilePart(batchId ?? string.Empty),
            _ => scope.ToString(),
        };
        return string.Create(CultureInfo.InvariantCulture, $"DS_DON_{part}_{workspace.Now:yyyyMMdd_HHmm}.csv");
    }

    /// <summary>Runs the export through <see cref="TikTokWorkspace.RunAsync"/>. Returns false when it failed.</summary>
    public Task<bool> ExportAsync(OrderListExportScope scope, string? batchId = null) =>
        workspace.RunAsync(
            "Đang xuất danh sách đơn...",
            cancellationToken => ExportCoreAsync(scope, batchId, cancellationToken),
            "ExportOrderList");

    private async Task ExportCoreAsync(OrderListExportScope scope, string? batchId, CancellationToken cancellationToken)
    {
        var services = workspace.Services;
        var context = new OrderListExportContext(
            workspace.ShopNames(),
            workspace.SkuMap(),
            workspace.Assets,
            workspace.ClassificationOptions(),
            TikTokLabels.Of,
            services.TimeProvider.LocalTimeZone);
        var exporter = services.OrderListExporter;
        var rows = await Task.Run(() => exporter.BuildRowsAsync(scope, batchId, context, cancellationToken), cancellationToken);
        if (rows.Count == 0)
        {
            workspace.StatusText = EmptyText;
            return;
        }

        var path = services.FileDialogs.PickSaveCsvFile(SuggestFileName(scope, batchId), workspace.Settings.OutputFolder);
        if (path is null)
        {
            workspace.StatusText = CancelledText;
            return;
        }

        await Task.Run(() => exporter.WriteAsync(path, rows, cancellationToken), cancellationToken);
        var orderCount = rows.Select(row => row.OrderId).Distinct(StringComparer.Ordinal).Count();
        workspace.StatusText = $"Đã xuất {rows.Count} dòng ({orderCount} đơn) ra {path}";
    }

    private static string SafeFilePart(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray());
    }
}
