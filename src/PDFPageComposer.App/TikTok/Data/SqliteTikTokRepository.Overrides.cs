using PDFPageComposer.App.TikTok.Models;

namespace PDFPageComposer.App.TikTok.Data;

/// <summary>Files chosen by hand for printed sets (table OrderFileOverride, schema version 4).</summary>
public sealed partial class SqliteTikTokRepository
{
    public async Task<IReadOnlyList<FileOverride>> GetFileOverridesAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var command = SqliteValues.CreateCommand(
            connection,
            null,
            """
            SELECT OrderId, SkuId, CopyIndex, Seq, PdfAssetPath, PagesUsed, FirstPage, FilePageCount
            FROM OrderFileOverride ORDER BY OrderId, SkuId, CopyIndex, Seq;
            """);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var overrides = new List<FileOverride>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            overrides.Add(new FileOverride(
                reader.GetString("OrderId"),
                reader.GetString("SkuId"),
                reader.GetInt32("CopyIndex"),
                reader.GetInt32("Seq"),
                reader.GetString("PdfAssetPath"),
                reader.GetInt32("PagesUsed"),
                reader.GetInt32("FirstPage"),
                reader.IsDBNull(reader.GetOrdinal("FilePageCount")) ? null : reader.GetInt32("FilePageCount")));
        }

        return overrides;
    }

    public async Task SaveFileOverridesAsync(
        string orderId,
        IReadOnlyCollection<FileOverride> overrides,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderId);
        ArgumentNullException.ThrowIfNull(overrides);
        if (overrides.Any(choice => !string.Equals(choice.OrderId, orderId, StringComparison.Ordinal)))
        {
            throw new ArgumentException("Mọi file tự chọn phải thuộc cùng một đơn.", nameof(overrides));
        }

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await BeginTransactionAsync(connection, cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(
            connection,
            transaction,
            "DELETE FROM OrderFileOverride WHERE OrderId = @orderId;",
            cancellationToken,
            ("@orderId", orderId)).ConfigureAwait(false);
        foreach (var choice in overrides)
        {
            await ExecuteAsync(
                connection,
                transaction,
                """
                INSERT INTO OrderFileOverride(OrderId, SkuId, CopyIndex, Seq, PdfAssetPath, PagesUsed, FirstPage, FilePageCount)
                VALUES(@orderId, @skuId, @copyIndex, @seq, @path, @pagesUsed, @firstPage, @filePageCount);
                """,
                cancellationToken,
                ("@orderId", choice.OrderId),
                ("@skuId", choice.SkuId),
                ("@copyIndex", choice.CopyIndex),
                ("@seq", choice.Seq),
                ("@path", choice.PdfAssetPath),
                ("@pagesUsed", choice.PagesUsed),
                ("@firstPage", choice.FirstPage),
                ("@filePageCount", choice.FilePageCount)).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
