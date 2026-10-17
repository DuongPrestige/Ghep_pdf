using System.IO;
using System.Text;
using Serilog;

namespace PDFPageComposer.App.TikTok.Export;

/// <summary>
/// Writes the order list as CSV for Excel: UTF-8 with BOM, CRLF, RFC 4180 quoting. The file is written to a temp
/// file next to the target and then moved over it, so a failure never leaves a half-written CSV.
/// </summary>
public static class OrderListCsvWriter
{
    private static readonly UTF8Encoding Utf8WithBom = new(encoderShouldEmitUTF8Identifier: true);

    /// <summary>Quotes a cell when it holds a comma, a quote or a line break (quotes doubled).</summary>
    public static string Escape(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.AsSpan().IndexOfAny(",\"\r\n") >= 0
            ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : value;
    }

    public static void Write(TextWriter writer, IReadOnlyList<OrderListExportRow> rows)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(rows);

        WriteLine(writer, OrderListExportRow.Headers);
        foreach (var row in rows)
        {
            WriteLine(writer, row.ToCells());
        }
    }

    public static async Task WriteFileAsync(string path, IReadOnlyList<OrderListExportRow> rows, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(rows);

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException($"Đường dẫn file CSV không hợp lệ: {path}");
        var tempPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            var builder = new StringBuilder();
            using (var stringWriter = new StringWriter(builder) { NewLine = "\r\n" })
            {
                Write(stringWriter, rows);
            }

            await File.WriteAllTextAsync(tempPath, builder.ToString(), Utf8WithBom, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(tempPath, fullPath, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                $"Không ghi được file CSV '{Path.GetFileName(fullPath)}'. Kiểm tra quyền ghi thư mục và đóng file nếu đang mở trong Excel.",
                exception);
        }
        finally
        {
            DeleteTempFile(tempPath);
        }
    }

    private static void WriteLine(TextWriter writer, IReadOnlyList<string> cells)
    {
        for (var index = 0; index < cells.Count; index++)
        {
            if (index > 0)
            {
                writer.Write(',');
            }

            writer.Write(Escape(cells[index]));
        }

        writer.Write("\r\n");
    }

    private static void DeleteTempFile(string tempPath)
    {
        try
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Leftover temp file only; the export result itself is already decided.
            Log.Warning("Deleting the temporary order list CSV failed with {ExceptionType}", exception.GetType().FullName);
        }
    }
}
