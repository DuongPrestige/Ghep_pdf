using System.IO;
using PDFPageComposer.App.TikTok.Import;

namespace PDFPageComposer.Tests.TikTok.Import;

public sealed class CsvParserTests
{
    private static List<IReadOnlyList<string>> Read(string text) =>
        CsvParser.ReadRecords(new StringReader(text)).ToList();

    [Fact]
    public void ReadRecords_splits_simple_crlf_records()
    {
        var records = Read("a,b,c\r\n1,2,3\r\n");

        Assert.Equal(2, records.Count);
        Assert.Equal(["a", "b", "c"], records[0]);
        Assert.Equal(["1", "2", "3"], records[1]);
    }

    [Fact]
    public void ReadRecords_supports_lf_and_missing_final_newline()
    {
        var records = Read("a,b\n1,2");

        Assert.Equal(2, records.Count);
        Assert.Equal(["1", "2"], records[1]);
    }

    [Fact]
    public void ReadRecords_handles_quoted_commas_newlines_and_escaped_quotes()
    {
        var records = Read("x,y\r\n\"a, b\",\"line1\r\nline2\"\r\n\"say \"\"hi\"\"\",\"\t\"\r\n");

        Assert.Equal(3, records.Count);
        Assert.Equal(["a, b", "line1\r\nline2"], records[1]);
        Assert.Equal(["say \"hi\"", "\t"], records[2]);
    }

    [Fact]
    public void ReadRecords_strips_bom_and_keeps_empty_fields()
    {
        var records = Read("﻿Order ID,,x\r\n,,\r\n");

        Assert.Equal(["Order ID", "", "x"], records[0]);
        Assert.Equal(["", "", ""], records[1]);
    }

    [Fact]
    public void ReadRecords_ignores_trailing_empty_line_but_reports_inner_blank_line()
    {
        var records = Read("a\r\n\r\nb\r\n");

        Assert.Equal(3, records.Count);
        Assert.Equal([""], records[1]);
        Assert.Equal(["b"], records[2]);
    }

    [Fact]
    public void ReadRecords_throws_on_unterminated_quote()
    {
        Assert.Throws<OrderSourceFormatException>(() => Read("a,\"b\r\n"));
    }

    [Fact]
    public void ReadRecords_returns_nothing_for_empty_input()
    {
        Assert.Empty(Read(string.Empty));
    }
}
