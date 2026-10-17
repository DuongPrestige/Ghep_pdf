namespace PDFPageComposer.App.TikTok.Import;

/// <summary>The order file cannot be imported at all (wrong format, missing columns, bad encoding).</summary>
public sealed class OrderSourceFormatException : Exception
{
    public OrderSourceFormatException(string message)
        : this(message, [], null)
    {
    }

    public OrderSourceFormatException(string message, Exception? innerException)
        : this(message, [], innerException)
    {
    }

    private OrderSourceFormatException(string message, IReadOnlyList<string> missingColumns, Exception? innerException)
        : base(message, innerException)
    {
        MissingColumns = missingColumns;
    }

    /// <summary>Required header names that were not found (empty for other format errors).</summary>
    public IReadOnlyList<string> MissingColumns { get; }

    public static OrderSourceFormatException ForMissingColumns(IReadOnlyList<string> missingColumns)
    {
        ArgumentNullException.ThrowIfNull(missingColumns);

        var message =
            $"File không đúng định dạng xuất đơn của Seller Center. Thiếu cột bắt buộc: {string.Join(", ", missingColumns)}.";
        return new OrderSourceFormatException(message, missingColumns.ToArray(), null);
    }
}
