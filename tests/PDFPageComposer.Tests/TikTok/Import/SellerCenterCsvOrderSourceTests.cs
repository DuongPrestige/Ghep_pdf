using System.IO;
using System.Text;
using PDFPageComposer.App.TikTok.Import;

namespace PDFPageComposer.Tests.TikTok.Import;

public sealed class SellerCenterCsvOrderSourceTests
{
    private const string Header =
        "Order ID,Order Status,SKU ID,Product Name,Variation,Quantity,Created Time,Recipient,Phone #,Detail Address,Seller Note";

    private static string SamplePath => Path.Combine(AppContext.BaseDirectory, "TestData", "tiktok", "orders-sample.csv");

    [Fact]
    public void Parse_sample_file_reads_all_lines_without_tabs()
    {
        using var reader = new StreamReader(SamplePath, Encoding.UTF8);

        var result = SellerCenterCsvOrderSource.Parse(reader);

        Assert.Equal(36, result.Lines.Count);
        Assert.Empty(result.Warnings);
        Assert.Equal(33, result.Lines.Select(line => line.OrderId).Distinct().Count());

        var first = result.Lines[0];
        Assert.Equal("586610736328377928", first.OrderId);
        Assert.Equal(new DateTimeOffset(2026, 10, 16, 0, 32, 51, TimeSpan.FromHours(7)), first.CreatedAt);
        Assert.Equal(1, first.SourceRowNumber);
        Assert.Equal(36, result.Lines[^1].SourceRowNumber);
        Assert.All(result.Lines, line =>
        {
            Assert.DoesNotContain('\t', line.OrderId);
            Assert.DoesNotContain('\t', line.SkuId);
            Assert.True(line.Quantity >= 1);
            Assert.Equal(TimeSpan.FromHours(7), line.CreatedAt.Offset);
        });

        var noted = result.Lines.First(line => line.OrderId == "586605624104421255");
        Assert.Equal("ĐÃ IN IVAN mini", noted.SellerNote);
    }

    [Fact]
    public void Parse_sample_file_never_exposes_personal_columns()
    {
        var personal = ReadPersonalCells();
        Assert.NotEmpty(personal);

        using var reader = new StreamReader(SamplePath, Encoding.UTF8);
        var result = SellerCenterCsvOrderSource.Parse(reader);

        foreach (var line in result.Lines)
        {
            var values = new[]
            {
                line.OrderId, line.StatusText, line.SubstatusText, line.SkuId, line.ProductName, line.VariationName,
                line.BuyerUsername, line.BuyerMessage, line.SellerNote, line.PackageId,
            };
            Assert.DoesNotContain(values, value => value is not null && personal.Contains(value));
        }
    }

    [Fact]
    public async Task ReadAsync_reads_file_and_uses_file_name()
    {
        var source = new SellerCenterCsvOrderSource(SamplePath);

        var result = await source.ReadAsync(CancellationToken.None);

        Assert.Equal("orders-sample.csv", source.Name);
        Assert.Equal(36, result.Lines.Count);
    }

    [Fact]
    public async Task ReadAsync_rejects_invalid_utf8()
    {
        var directory = Directory.CreateTempSubdirectory("gpdf-import-");
        try
        {
            var path = Path.Combine(directory.FullName, "bad.csv");
            var bytes = Encoding.UTF8.GetBytes(Header + "\r\n1,Cần vận chuyển,S1,P,");
            await File.WriteAllBytesAsync(path, [.. bytes, 0xFF, 0xFE, 0x41, (byte)'\r', (byte)'\n']);

            var exception = await Assert.ThrowsAsync<OrderSourceFormatException>(
                () => new SellerCenterCsvOrderSource(path).ReadAsync(CancellationToken.None));
            Assert.Contains("UTF-8", exception.Message);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void Parse_maps_columns_by_header_name_regardless_of_order_and_case()
    {
        const string csv =
            "  created time ,quantity,VARIATION,product name,sku id,order status,ORDER ID,Buyer Message\r\n" +
            "1/2/2026 7:05:09,2\t,\"20 CARD, 1 Mặt\",Card,111\t,Cần vận chuyển,999\t,\"\t\"\r\n";

        var result = SellerCenterCsvOrderSource.Parse(new StringReader(csv));

        var line = Assert.Single(result.Lines);
        Assert.Equal("999", line.OrderId);
        Assert.Equal("111", line.SkuId);
        Assert.Equal(2, line.Quantity);
        Assert.Equal("20 CARD, 1 Mặt", line.VariationName);
        Assert.Null(line.BuyerMessage);
        Assert.Null(line.SellerNote);
        Assert.Equal(new DateTimeOffset(2026, 2, 1, 7, 5, 9, TimeSpan.FromHours(7)), line.CreatedAt);
    }

    [Fact]
    public void Parse_throws_listing_missing_required_columns()
    {
        const string csv = "Order ID,Order Status,Product Name,Variation,Created Time\r\n1,x,p,v,01/01/2026 00:00:00\r\n";

        var exception = Assert.Throws<OrderSourceFormatException>(
            () => SellerCenterCsvOrderSource.Parse(new StringReader(csv)));

        Assert.Equal(["SKU ID", "Quantity"], exception.MissingColumns);
        Assert.Contains("SKU ID", exception.Message);
        Assert.Contains("Quantity", exception.Message);
        Assert.Contains("Thiếu cột bắt buộc", exception.Message);
    }

    [Fact]
    public void Parse_skips_invalid_rows_with_warnings_and_ignores_blank_rows()
    {
        var csv = string.Join("\r\n",
            Header,
            "1\t,Cần vận chuyển,S1\t,P,V,1\t,16/10/2026 10:00:00\t,A,B,C,",
            ",,,,,,,,,,",
            "2\t,Cần vận chuyển,\"\t\",P,V,1\t,16/10/2026 10:00:00\t,A,B,C,",
            "3\t,Cần vận chuyển,S3\t,P,V,0\t,16/10/2026 10:00:00\t,A,B,C,",
            "4\t,Cần vận chuyển,S4\t,P,V,abc,16/10/2026 10:00:00\t,A,B,C,",
            "5\t,Cần vận chuyển,S5\t,P,V,1\t,2026-10-16 10:00,A,B,C,",
            "6\t,Cần vận chuyển,S6\t,P,V,1\t,16/10/2026 10:00:00\t,A,B,C,ĐÃ IN",
            string.Empty);

        var result = SellerCenterCsvOrderSource.Parse(new StringReader(csv));

        Assert.Equal(["1", "6"], result.Lines.Select(line => line.OrderId));
        Assert.Equal([1, 7], result.Lines.Select(line => line.SourceRowNumber));
        Assert.Equal(8, result.Warnings.Count);
        Assert.StartsWith("Dòng 3", result.Warnings[0]);
        Assert.StartsWith("Dòng 4", result.Warnings[1]);
        Assert.StartsWith("Dòng 5", result.Warnings[2]);
        Assert.StartsWith("Dòng 6", result.Warnings[3]);
        Assert.StartsWith("Đơn 2: có dòng dữ liệu lỗi", result.Warnings[4]);
        Assert.Equal(["2", "3", "4", "5"], result.RejectedOrderIds);
        Assert.Equal("ĐÃ IN", result.Lines[1].SellerNote);
    }

    [Fact]
    public void Parse_drops_the_whole_order_when_one_of_its_rows_is_invalid()
    {
        var csv = string.Join("\r\n",
            Header,
            "7\t,Cần vận chuyển,IVAN20\t,P,20 CARD,1\t,16/10/2026 10:00:00\t,A,B,C,",
            "7\t,Cần vận chuyển,CODY30\t,P,30 CARD,1.0,16/10/2026 10:00:00\t,A,B,C,",
            "8\t,Cần vận chuyển,IVAN20\t,P,20 CARD,1\t,16/10/2026 10:05:00\t,A,B,C,",
            string.Empty);

        var result = SellerCenterCsvOrderSource.Parse(new StringReader(csv));

        Assert.Equal(["8"], result.Lines.Select(line => line.OrderId));
        Assert.Equal(["7"], result.RejectedOrderIds);
        Assert.Single(result.Warnings, warning => warning.StartsWith("Đơn 7: có dòng dữ liệu lỗi nên cả đơn bị bỏ qua", StringComparison.Ordinal));
    }

    private static HashSet<string> ReadPersonalCells()
    {
        string[] personalHeaders =
        [
            "Recipient", "Phone #", "Province", "District", "Commune", "Detail Address", "Additional address information",
        ];

        using var reader = new StreamReader(SamplePath, Encoding.UTF8);
        var records = CsvParser.ReadRecords(reader).ToList();
        var header = records[0].Select(name => name.Trim()).ToList();
        var indexes = personalHeaders.Select(name => header.IndexOf(name)).Where(index => index >= 0).ToArray();
        Assert.Equal(personalHeaders.Length, indexes.Length);

        return records
            .Skip(1)
            .SelectMany(record => indexes.Where(index => index < record.Count).Select(index => record[index].Trim()))
            .Where(value => value.Length > 2)
            .ToHashSet(StringComparer.Ordinal);
    }
}
