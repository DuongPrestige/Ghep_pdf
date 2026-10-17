using PDFPageComposer.App.TikTok.Matching;

namespace PDFPageComposer.Tests.TikTok.Matching;

public sealed class PdfAssetNameParserTests
{
    private static readonly DateOnly Today = new(2026, 10, 16);

    [Theory]
    [InlineData("JACK - 3 PAGE - 13.10.pdf", "JACK", 3, 2026, 10, 13)]
    [InlineData("24AT - 3 PAGES - 13.10.pdf", "24AT", 3, 2026, 10, 13)]
    [InlineData("cody nam võ - 4 page - 14.10.PDF", "CODY NAM VO", 4, 2026, 10, 14)]
    [InlineData("IVAN-3PAGE-1.9.pdf", "IVAN", 3, 2026, 9, 1)]
    [InlineData("SƠN.K   -   12 Pages   -  05.10.pdf", "SON.K", 12, 2026, 10, 5)]
    [InlineData(@"C:\lib\sub\IVAN - 3 PAGE - 16.10.pdf", "IVAN", 3, 2026, 10, 16)]
    [InlineData("IVAN - 3 PAGE - 17.10.pdf", "IVAN", 3, 2025, 10, 17)]
    [InlineData("QUANG HUNG - 3 PAGE - 17.09 (2).pdf", "QUANG HUNG", 3, 2026, 9, 17)]
    [InlineData("CONGB - 1 PAGE - 23.08(3).pdf", "CONGB", 1, 2026, 8, 23)]
    public void TryParse_accepts_valid_names(string fileName, string idol, int pages, int year, int month, int day)
    {
        var ok = PdfAssetNameParser.TryParse(fileName, Today, out var parsedIdol, out var pageCount, out var fileDate);

        Assert.True(ok);
        Assert.Equal(idol, parsedIdol);
        Assert.Equal(pages, pageCount);
        Assert.Equal(new DateOnly(year, month, day), fileDate);
    }

    [Fact]
    public void TryParse_uses_previous_year_across_new_year()
    {
        var ok = PdfAssetNameParser.TryParse("IVAN - 3 PAGE - 28.12.pdf", new DateOnly(2026, 1, 5), out _, out _, out var fileDate);

        Assert.True(ok);
        Assert.Equal(new DateOnly(2025, 12, 28), fileDate);
    }

    [Theory]
    [InlineData("IVAN - 3 PAGE - 31.02.pdf")]
    [InlineData("IVAN - 3 PAGE - 10.13.pdf")]
    [InlineData("IVAN - 3 PAGE - 00.10.pdf")]
    [InlineData("IVAN - 0 PAGE - 10.10.pdf")]
    [InlineData("IVAN - 3 PAGE.pdf")]
    [InlineData("IVAN 3 PAGE 10.10.pdf")]
    [InlineData("IVAN - 3 PAGE - 10.10 (a).pdf")]
    [InlineData("IVAN - 3 PAGE - 10.10.png")]
    [InlineData(" - 3 PAGE - 10.10.pdf")]
    [InlineData("")]
    public void TryParse_rejects_invalid_names(string fileName)
    {
        var ok = PdfAssetNameParser.TryParse(fileName, Today, out var idol, out var pageCount, out var fileDate);

        Assert.False(ok);
        Assert.Equal(string.Empty, idol);
        Assert.Equal(0, pageCount);
        Assert.Equal(default, fileDate);
    }
}
