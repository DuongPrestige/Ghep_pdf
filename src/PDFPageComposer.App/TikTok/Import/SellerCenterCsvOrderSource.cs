using System.Globalization;
using System.IO;
using System.Text;
using PDFPageComposer.App.TikTok.Models;

namespace PDFPageComposer.App.TikTok.Import;

/// <summary>
/// Reads the "Chờ vận chuyển" order export of TikTok Seller Center (TECH_SPEC_TIKTOK 3.1, 3.2).
/// Columns are mapped by header name. Recipient, phone and address columns are never read (AC-T07).
/// </summary>
public sealed class SellerCenterCsvOrderSource : IOrderSource
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);
    private static readonly string[] CreatedTimeFormats = ["dd/MM/yyyy HH:mm:ss", "d/M/yyyy H:mm:ss"];

    private readonly string _filePath;

    public SellerCenterCsvOrderSource(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        _filePath = filePath;
        Name = Path.GetFileName(filePath);
    }

    public string Name { get; }

    public Task<OrderSourceResult> ReadAsync(CancellationToken cancellationToken) =>
        Task.Run(() => ReadFile(_filePath, cancellationToken), cancellationToken);

    /// <summary>Parses an already opened CSV export.</summary>
    /// <exception cref="OrderSourceFormatException">The header misses a required column or the CSV is malformed.</exception>
    public static OrderSourceResult Parse(TextReader reader) => Parse(reader, CancellationToken.None);

    private static OrderSourceResult ReadFile(string filePath, CancellationToken cancellationToken)
    {
        var strictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            bufferSize: 64 * 1024,
            FileOptions.SequentialScan);
        using var reader = new StreamReader(stream, strictUtf8, detectEncodingFromByteOrderMarks: true);

        try
        {
            return Parse(reader, cancellationToken);
        }
        catch (DecoderFallbackException exception)
        {
            throw new OrderSourceFormatException(
                "File chứa ký tự không hợp lệ (không phải UTF-8). Hãy tải lại file xuất đơn từ Seller Center.",
                exception);
        }
    }

    private static OrderSourceResult Parse(TextReader reader, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reader);

        using var records = CsvParser.ReadRecords(reader).GetEnumerator();
        if (!records.MoveNext())
        {
            throw new OrderSourceFormatException("File trống, không có dòng tiêu đề.");
        }

        var columns = SellerCenterColumnMap.Create(records.Current);
        var lines = new List<ImportedOrderLine>();
        var warnings = new List<string>();
        var rejectedOrderIds = new List<string>();
        var rowNumber = 0;

        while (records.MoveNext())
        {
            cancellationToken.ThrowIfCancellationRequested();
            rowNumber++;

            var record = records.Current;
            if (IsBlank(record))
            {
                continue;
            }

            var line = TryCreateLine(record, columns, rowNumber, out var warning, out var rejectedOrderId);
            if (line is null)
            {
                warnings.Add(warning ?? $"Dòng {rowNumber}: dữ liệu không hợp lệ, đã bỏ qua.");
                if (rejectedOrderId is not null && !rejectedOrderIds.Contains(rejectedOrderId, StringComparer.Ordinal))
                {
                    rejectedOrderIds.Add(rejectedOrderId);
                }

                continue;
            }

            lines.Add(line);
        }

        // A row that cannot be read blocks its whole order, so the order is never printed half (TT-08).
        if (rejectedOrderIds.Count > 0)
        {
            var rejected = rejectedOrderIds.ToHashSet(StringComparer.Ordinal);
            lines.RemoveAll(line => rejected.Contains(line.OrderId));
            warnings.AddRange(rejectedOrderIds.Select(orderId =>
                $"Đơn {orderId}: có dòng dữ liệu lỗi nên cả đơn bị bỏ qua, không ghép để tránh in thiếu. Hãy kiểm tra file xuất đơn."));
        }

        return new OrderSourceResult(lines, warnings) { RejectedOrderIds = rejectedOrderIds };
    }

    private static ImportedOrderLine? TryCreateLine(
        IReadOnlyList<string> record,
        SellerCenterColumnMap columns,
        int rowNumber,
        out string? warning,
        out string? rejectedOrderId)
    {
        warning = null;
        var orderId = SellerCenterColumnMap.Get(record, columns.OrderId);
        rejectedOrderId = orderId;
        var skuId = SellerCenterColumnMap.Get(record, columns.SkuId);
        if (orderId is null || skuId is null)
        {
            warning = $"Dòng {rowNumber}: thiếu mã đơn hoặc SKU ID, đã bỏ qua.";
            return null;
        }

        var quantityText = SellerCenterColumnMap.Get(record, columns.Quantity);
        if (!int.TryParse(quantityText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var quantity) ||
            quantity < 1)
        {
            warning = $"Dòng {rowNumber} (đơn {orderId}): số lượng \"{quantityText}\" không hợp lệ, đã bỏ qua.";
            return null;
        }

        var createdText = SellerCenterColumnMap.Get(record, columns.CreatedTime);
        if (!DateTime.TryParseExact(
                createdText,
                CreatedTimeFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var createdLocal))
        {
            warning = $"Dòng {rowNumber} (đơn {orderId}): thời gian tạo đơn \"{createdText}\" không hợp lệ, đã bỏ qua.";
            return null;
        }

        rejectedOrderId = null;
        return new ImportedOrderLine(
            OrderId: orderId,
            StatusText: SellerCenterColumnMap.Get(record, columns.OrderStatus) ?? string.Empty,
            SubstatusText: SellerCenterColumnMap.Get(record, columns.OrderSubstatus),
            SkuId: skuId,
            ProductName: SellerCenterColumnMap.Get(record, columns.ProductName) ?? string.Empty,
            VariationName: SellerCenterColumnMap.Get(record, columns.Variation) ?? string.Empty,
            Quantity: quantity,
            CreatedAt: new DateTimeOffset(DateTime.SpecifyKind(createdLocal, DateTimeKind.Unspecified), VietnamOffset),
            BuyerUsername: SellerCenterColumnMap.Get(record, columns.BuyerUsername),
            BuyerMessage: SellerCenterColumnMap.Get(record, columns.BuyerMessage),
            SellerNote: SellerCenterColumnMap.Get(record, columns.SellerNote),
            PackageId: SellerCenterColumnMap.Get(record, columns.PackageId),
            SourceRowNumber: rowNumber);
    }

    private static bool IsBlank(IReadOnlyList<string> record)
    {
        foreach (var cell in record)
        {
            if (TextNormalizer.TrimCell(cell) is not null)
            {
                return false;
            }
        }

        return true;
    }
}
