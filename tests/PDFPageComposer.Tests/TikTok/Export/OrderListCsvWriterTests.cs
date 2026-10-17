using System.IO;
using System.Text;
using PDFPageComposer.App.TikTok.Export;
using PDFPageComposer.App.TikTok.Import;

namespace PDFPageComposer.Tests.TikTok.Export;

public sealed class OrderListCsvWriterTests : IDisposable
{
    private readonly string tempDirectory =
        Path.Combine(Path.GetTempPath(), "gpdf-csv-tests", Guid.NewGuid().ToString("N"));

    public OrderListCsvWriterTests()
    {
        Directory.CreateDirectory(tempDirectory);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
        catch (IOException exception)
        {
            Console.WriteLine($"Temp cleanup failed: {exception.Message}");
        }
    }

    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("a,b", "\"a,b\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("line1\nline2", "\"line1\nline2\"")]
    [InlineData("line1\r\nline2", "\"line1\r\nline2\"")]
    [InlineData("123\t", "123\t")]
    [InlineData("", "")]
    public void Escape_quotes_only_when_needed(string value, string expected)
    {
        Assert.Equal(expected, OrderListCsvWriter.Escape(value));
    }

    [Fact]
    public void Write_emits_header_and_rows_with_crlf()
    {
        var writer = new StringWriter { NewLine = "\n" };

        OrderListCsvWriter.Write(writer, [Row("1", buyerMessage: "a, \"b\"\nc")]);

        var text = writer.ToString();
        Assert.StartsWith("Order ID,Shop,Thời gian đặt,Đã làm file in,", text, StringComparison.Ordinal);
        Assert.EndsWith(",\"a, \"\"b\"\"\nc\",note\r\n", text, StringComparison.Ordinal);
        Assert.Equal(2, text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Theory]
    [InlineData("- giao giờ hành chính", "'- giao giờ hành chính")]
    [InlineData("+84901234567", "'+84901234567")]
    [InlineData("=))", "'=))")]
    [InlineData("@shop", "'@shop")]
    [InlineData("giao nhanh - gấp", "giao nhanh - gấp")]
    [InlineData("", "")]
    public void Free_text_cells_starting_with_a_formula_character_get_a_text_prefix(string note, string expected)
    {
        var writer = new StringWriter();

        OrderListCsvWriter.Write(writer, [Row("1", buyerMessage: note) with { SellerNote = note, ProductName = note }]);

        using var reader = new StringReader(writer.ToString());
        var record = CsvParser.ReadRecords(reader).Skip(1).Single();
        Assert.Equal(expected, record[10]);
        Assert.Equal(expected, record[17]);
        Assert.Equal(expected, record[18]);
        Assert.Equal("1\t", record[0]);
        Assert.Equal("SKU1\t", record[9]);
    }

    [Fact]
    public async Task Write_file_uses_utf8_bom_and_round_trips_through_the_csv_parser()
    {
        var path = Path.Combine(tempDirectory, "ds.csv");
        File.WriteAllText(path, "old content");
        var rows = new[]
        {
            Row("586621080534091589", buyerMessage: "cho em ảnh khác, nhé\r\n\"gấp\""),
            Row("586621080534091590"),
        };

        await OrderListCsvWriter.WriteFileAsync(path, rows, CancellationToken.None);

        var bytes = await File.ReadAllBytesAsync(path);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3));
        var text = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        Assert.EndsWith("\r\n", text, StringComparison.Ordinal);
        Assert.DoesNotContain("old content", text, StringComparison.Ordinal);

        using var reader = new StringReader(text);
        var records = CsvParser.ReadRecords(reader).ToList();
        Assert.Equal(3, records.Count);
        Assert.Equal(OrderListExportRow.Headers, records[0]);
        Assert.Equal("586621080534091589\t", records[1][0]);
        Assert.Equal("cho em ảnh khác, nhé\r\n\"gấp\"", records[1][17]);
        Assert.Equal(["ds.csv"], Directory.GetFiles(tempDirectory).Select(Path.GetFileName));
    }

    [Fact]
    public async Task Write_file_failure_reports_vietnamese_text_and_leaves_no_file()
    {
        var path = Path.Combine(tempDirectory, "missing-folder", "ds.csv");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => OrderListCsvWriter.WriteFileAsync(path, [Row("1")], CancellationToken.None));

        Assert.Contains("Không ghi được file CSV", exception.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(path));
        Assert.Empty(Directory.GetFiles(tempDirectory, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Cancelled_write_keeps_the_previous_file_and_removes_the_temp_file()
    {
        var path = Path.Combine(tempDirectory, "ds.csv");
        File.WriteAllText(path, "old content");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => OrderListCsvWriter.WriteFileAsync(path, [Row("1")], cancellation.Token));

        Assert.Equal("old content", File.ReadAllText(path));
        Assert.Equal(["ds.csv"], Directory.GetFiles(tempDirectory).Select(Path.GetFileName));
    }

    private static OrderListExportRow Row(string orderId, string buyerMessage = "") =>
        new(
            orderId,
            "Shop A",
            "16/10/2026 08:01:00",
            "Có",
            "Lô in",
            "B1",
            "16/10/2026 09:00:00",
            "Đã tạo file in",
            "Đã in (lô B1)",
            "SKU1",
            "[CARD] Ivan",
            "20 CARD, 1 Mặt",
            "1",
            "IVAN - 3 PAGE - 15.10.pdf",
            string.Empty,
            "Có",
            "b***r",
            buyerMessage,
            "note");
}
