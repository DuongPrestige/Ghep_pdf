using PDFPageComposer.App.TikTok.Matching;
using PDFPageComposer.App.TikTok.Models;

namespace PDFPageComposer.Tests.TikTok.Matching;

public sealed class SkuConfigSuggesterTests
{
    private static readonly IdolAlias[] Aliases = [new IdolAlias("COMBO24ANHTRAI", "24AT")];

    [Theory]
    [InlineData("[CARD BO GÓC CODY NAM VÕ] - Card bo góc", "30 CARD, 1 Mặt", SkuProcessType.Auto, 30, "CODY NAM VO")]
    [InlineData("[CARD BO GÓC Vỹ Vỹ] - Card bo góc", "100 CARD, 1 Mặt", SkuProcessType.Auto, 100, "VY VY")]
    [InlineData("[CARD CAPTAIN BOY BO GÓC] - Card bo góc", "20 CARD, 1 Mặt", SkuProcessType.Auto, 20, "CAPTAIN BOY")]
    [InlineData("[CARD BO GÓC TINH HÀ SAY HI] - Card", "50 CARD, 1 MẶT, COMBO 24 ANH TRAI", SkuProcessType.Auto, 50, "24AT")]
    [InlineData("[CARD BO GÓC TINH HÀ SAY HI] - Card", "20 CARD, 1 MẶT, Jaysonlei", SkuProcessType.Auto, 20, "JAYSONLEI")]
    [InlineData("CARD THẺ GÓC NGUYỄN ĐÌNH BẮC - TẶNG THẺ CHỮ KÝ - PHOTOCARD CẦU THỦ ÉP PLASTIC - NHIỀU MẪU KHÔNG TRÙNG", "20 CARD, 1 Mặt", SkuProcessType.Auto, 20, "NGUYEN DINH BAC")]
    [InlineData("CARD BO GÓC Jack 97 - J97 - TẶNG CHỮ KÝ JACK - NHIỀU HÌNH KHÔNG TRÙNG - COMBO THẺ BO GÓC - LUÔN UPDATE ẢNH MỚI", "50 CARD, 1 Mặt", SkuProcessType.Auto, 50, "JACK 97")]
    [InlineData("CARD BO GÓC Dillan Hoàng Phan- ATSH - Ép Plastic Bền Đẹp", "20 CARD", SkuProcessType.Auto, 20, "DILLAN HOANG PHAN")]
    [InlineData("[CARD BO GÓC TINH HÀ SAY HI] - CẬP NHẬT ẢNH MỚI - SET PHÒNG TẮM", "20 CARD, 1 MẶT, Đặng Hồng Hải", SkuProcessType.Auto, 20, "DANG HONG HAI")]
    public void Suggest_returns_auto_with_card_count_and_idol(
        string product,
        string variation,
        SkuProcessType expectedType,
        int expectedCards,
        string expectedIdol)
    {
        var suggestion = new SkuConfigSuggester().Suggest(product, variation, Aliases);

        Assert.Equal(expectedType, suggestion.ProcessType);
        Assert.Equal(expectedCards, suggestion.CardCount);
        // "20 CARD" without "1 MẶT" has no side count but is still the plain one-sided set.
        Assert.Equal(variation.Contains("MẶT", StringComparison.OrdinalIgnoreCase) ? 1 : null, suggestion.Sides);
        Assert.Equal(expectedIdol, suggestion.Idol);
    }

    [Theory]
    [InlineData("[CARD BO GÓC IVAN] - Card bo góc", "20 CARD, 2 MẶT (MẶT ẢNH + CHỮ KÝ)")]
    [InlineData("[CARD BO GÓC TINH HÀ SAY HI] - Card", "20 CARD, 1 MẶT, IN THEO YÊU CẦU (GỬI ẢNH CHO SHOP)")]
    [InlineData("CARD BO GÓC A4 A5 KHỔNG LỒ - Decal", "1 mặt, A4 (decal bóc dán)")]
    [InlineData("CARD BO GÓC CORTIS - Martin/James/Juhoon ĐỦ THÀNH VIÊN - TẶNG CHỮ KÝ", "40 CARD, 1 Mặt, Ảnh Yêu cầu (gửi cho shop)")]
    [InlineData("IN THEO YÊU CẦU - COMBO CARD BO GÓC - 1 MẶT 2 MẶT - ÉP PLASTIC - [TẶNG ẢNH CHỮ KÝ IDOL]", "40 CARD, 1 Mặt")]
    [InlineData("[CARD CAPTAIN BOY BO GÓC] - TINH HÀ SAY HI 2026", "20 CARD, 2 Mặt (ẢNH KHÁC NHAU)")]
    public void Suggest_returns_manual(string product, string variation)
    {
        var suggestion = new SkuConfigSuggester().Suggest(product, variation, Aliases);

        Assert.Equal(SkuProcessType.Manual, suggestion.ProcessType);
    }

    [Fact]
    public void Suggest_manual_two_sided_still_suggests_idol_from_product()
    {
        var suggestion = new SkuConfigSuggester().Suggest("[CARD BO GÓC IVAN] - Card bo góc", "20 CARD, 2 MẶT (MẶT ẢNH + CHỮ KÝ)", Aliases);

        Assert.Equal("IVAN", suggestion.Idol);
        Assert.Equal(20, suggestion.CardCount);
        Assert.Equal(2, suggestion.Sides);
    }

    [Theory]
    [InlineData("[MÓC KHÓA ALBUM MINI] - Móc khóa", "Tất Cả Các Khung, IVan")]
    [InlineData("[Ảnh Polar IG] - Polaroid", "18 CARD, QUANG HÙNG MASTER-D, khung ngẫu nhiên (tổng hợp)")]
    [InlineData("[PTB Tinh Hà Say Hi 2026] - Photobooth", "10 PHOTOBOOTH, Quang Hùng MasterD, Dọc 1x4 ảnh")]
    [InlineData("MÓC KHÓA - IN HÌNH THEO YÊU CẦU - MÓC KHÓA ANH TRAI SAY HI - IN 2 MẶT", "IDOL KHÁC/IN THEO YÊU CẦU (GỬI ẢNH CHO SHOP), 2 mặt giống nhau")]
    public void Suggest_returns_ignore_without_idol(string product, string variation)
    {
        var suggestion = new SkuConfigSuggester().Suggest(product, variation, Aliases);

        Assert.Equal(SkuProcessType.Ignore, suggestion.ProcessType);
        Assert.Null(suggestion.Idol);
    }

    [Fact]
    public void Suggest_maps_alias_by_idol_key()
    {
        var aliases = new[] { new IdolAlias("VYVY", "VYVY") };

        var suggestion = new SkuConfigSuggester().Suggest("[CARD BO GÓC Vỹ Vỹ] - Card", "20 CARD, 1 Mặt", aliases);

        Assert.Equal("VYVY", suggestion.Idol);
    }
}
