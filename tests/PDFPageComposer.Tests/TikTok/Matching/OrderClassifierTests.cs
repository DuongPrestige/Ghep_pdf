using PDFPageComposer.App.TikTok;
using PDFPageComposer.App.TikTok.Matching;
using PDFPageComposer.App.TikTok.Models;
using static PDFPageComposer.Tests.TikTok.Matching.MatchingTestData;

namespace PDFPageComposer.Tests.TikTok.Matching;

public sealed class OrderClassifierTests
{
    private static readonly IReadOnlyDictionary<string, SkuConfig> DefaultSkus = Skus(
        Auto("IVAN20", "IVAN", 20),
        Auto("CODY30", "CODY", 30),
        Manual("IVAN2M"),
        Ignore("KEYCHAIN"),
        Ignore("NEWIGNORE", SkuConfigStatus.Unconfigured),
        Sku("UNCONF", SkuProcessType.Auto, SkuConfigStatus.Unconfigured, "IVAN", 20),
        Sku("NOIDOL", SkuProcessType.Auto, SkuConfigStatus.Confirmed, null, 20),
        Sku("NOCARD", SkuProcessType.Auto, SkuConfigStatus.Confirmed, "IVAN", 0));

    private static readonly IReadOnlyList<PdfAsset> DefaultAssets = [Asset("IVAN", 3, 15), Asset("Ivan", 3, 10)];

    private static readonly ClassificationOptions Defaults = new();

    private static OrderClassification Classify(TikTokOrder order, ClassificationOptions? options = null) =>
        new OrderClassifier().Classify(order, DefaultSkus, DefaultAssets, options ?? Defaults);

    [Fact]
    public void Ready_when_every_card_line_has_a_file_and_ignore_lines_do_not_block()
    {
        var result = Classify(Order("O1", 0, [("IVAN20", 2), ("KEYCHAIN", 1)]));

        Assert.Equal(OrderCategory.Ready, result.Category);
        Assert.Empty(result.Reasons);
    }

    [Fact]
    public void Printed_by_batch_seller_note_source_or_note_marker()
    {
        var byBatch = Classify(Order("O1", 0, [("IVAN20", 1)], printedBatchId: "B1", printedSource: PrintedSource.Batch));
        var bySource = Classify(Order("O2", 0, [("IVAN20", 1)], printedSource: PrintedSource.SellerNote));
        var byNote = Classify(Order("O3", 0, [("IVAN20", 1)], sellerNote: "Đã in IVAN mini"));

        Assert.Equal(OrderCategory.Printed, byBatch.Category);
        Assert.Contains("Đã in (lô B1)", byBatch.Reasons);
        Assert.Equal(OrderCategory.Printed, bySource.Category);
        Assert.Contains("Đã in theo Seller Note", bySource.Reasons);
        Assert.Equal(OrderCategory.Printed, byNote.Category);
        Assert.Contains("Đã in theo Seller Note", byNote.Reasons);
    }

    [Fact]
    public void Printed_has_priority_over_no_card_needed()
    {
        var result = Classify(Order("O1", 0, [("KEYCHAIN", 1)], printedBatchId: "B1"));

        Assert.Equal(OrderCategory.Printed, result.Category);
    }

    [Fact]
    public void Seller_note_marker_is_a_note_when_option_is_disabled()
    {
        var result = Classify(
            Order("O1", 0, [("IVAN20", 1)], sellerNote: "ĐÃ IN"),
            new ClassificationOptions(SellerNoteMarksPrinted: false));

        Assert.Equal(OrderCategory.HasNote, result.Category);
    }

    [Fact]
    public void Allow_reprint_skips_printed_state()
    {
        var result = Classify(
            Order("O1", 0, [("IVAN20", 1)], printedBatchId: "B1", printedSource: PrintedSource.Batch),
            new ClassificationOptions(AllowReprint: true));

        Assert.Equal(OrderCategory.Ready, result.Category);
    }

    [Fact]
    public void Allow_reprint_on_printed_order_skips_the_note_gate()
    {
        var result = Classify(
            Order("O1", 0, [("IVAN20", 1)], sellerNote: "ĐÃ IN", printedBatchId: "B1", printedSource: PrintedSource.Batch),
            new ClassificationOptions(AllowReprint: true));

        Assert.Equal(OrderCategory.Ready, result.Category);
    }

    [Fact]
    public void Notes_do_not_block_when_note_rules_are_turned_off()
    {
        var options = new ClassificationOptions(SellerNoteMarksPrinted: false, BlockOrdersWithNotes: false);

        var withMessage = Classify(Order("O1", 0, [("IVAN20", 1)], buyerMessage: "cho em ảnh khác"), options);
        var withMarker = Classify(Order("O2", 0, [("IVAN20", 1)], sellerNote: "ĐÃ IN IVAN mini"), options);

        Assert.Equal(OrderCategory.Ready, withMessage.Category);
        Assert.Equal(OrderCategory.Ready, withMarker.Category);
    }

    [Fact]
    public void Allow_reprint_still_blocks_unprinted_order_with_note()
    {
        var result = Classify(
            Order("O1", 0, [("IVAN20", 1)], buyerMessage: "cho em ảnh khác"),
            new ClassificationOptions(AllowReprint: true));

        Assert.Equal(OrderCategory.HasNote, result.Category);
    }

    [Fact]
    public void Allow_reprint_on_printed_order_still_checks_configuration()
    {
        var result = Classify(
            Order("O1", 0, [("IVAN2M", 1)], printedBatchId: "B1", printedSource: PrintedSource.Batch),
            new ClassificationOptions(AllowReprint: true));

        Assert.Equal(OrderCategory.Manual, result.Category);
    }

    [Fact]
    public void Stored_seller_note_mark_is_ignored_when_option_is_disabled()
    {
        var result = Classify(
            Order("O1", 0, [("IVAN20", 1)], sellerNote: "ĐÃ IN IVAN mini", printedSource: PrintedSource.SellerNote),
            new ClassificationOptions(SellerNoteMarksPrinted: false));

        Assert.Equal(OrderCategory.HasNote, result.Category);
    }

    [Fact]
    public void No_card_needed_when_only_confirmed_ignore_lines_even_with_note()
    {
        var result = Classify(Order("O1", 0, [("KEYCHAIN", 2)], buyerMessage: "giao nhanh nhé"));

        Assert.Equal(OrderCategory.NoCardNeeded, result.Category);
    }

    [Fact]
    public void Unconfirmed_ignore_suggestion_still_needs_config()
    {
        var result = Classify(Order("O1", 0, [("NEWIGNORE", 1)]));

        Assert.Equal(OrderCategory.NeedsConfig, result.Category);
        Assert.Contains(result.Reasons, reason => reason.Contains("SKU NEWIGNORE chưa được cấu hình", StringComparison.Ordinal));
    }

    [Fact]
    public void Has_note_for_buyer_message_or_seller_note_and_shows_text()
    {
        var buyer = Classify(Order("O1", 0, [("IVAN20", 1)], buyerMessage: "cho em ảnh khác nhé"));
        var seller = Classify(Order("O2", 0, [("IVAN20", 1)], sellerNote: "dã có qhung"));

        Assert.Equal(OrderCategory.HasNote, buyer.Category);
        Assert.Contains(buyer.Reasons, reason => reason.Contains("cho em ảnh khác nhé", StringComparison.Ordinal));
        Assert.Equal(OrderCategory.HasNote, seller.Category);
        Assert.Contains(seller.Reasons, reason => reason.Contains("dã có qhung", StringComparison.Ordinal));
    }

    [Fact]
    public void Whitespace_only_notes_are_ignored()
    {
        var result = Classify(Order("O1", 0, [("IVAN20", 1)], buyerMessage: " \t ", sellerNote: "\t"));

        Assert.Equal(OrderCategory.Ready, result.Category);
    }

    [Fact]
    public void Reviewed_note_is_allowed_until_the_note_changes()
    {
        var hash = OrderNotes.ComputeHash("cho em ảnh khác nhé", null);

        var reviewed = Classify(Order("O1", 0, [("IVAN20", 1)], buyerMessage: "cho em ảnh khác nhé", noteReviewedHash: hash));
        var changed = Classify(Order("O1", 0, [("IVAN20", 1)], buyerMessage: "đổi ý rồi", noteReviewedHash: hash));

        Assert.Equal(OrderCategory.Ready, reviewed.Category);
        Assert.Equal(OrderCategory.HasNote, changed.Category);
    }

    [Fact]
    public void Has_note_has_priority_over_needs_config()
    {
        var result = Classify(Order("O1", 0, [("UNCONF", 1)], buyerMessage: "hi"));

        Assert.Equal(OrderCategory.HasNote, result.Category);
    }

    [Theory]
    [InlineData("UNCONF", "chưa được cấu hình")]
    [InlineData("MISSING", "chưa có trong danh sách cấu hình")]
    [InlineData("NOIDOL", "thiếu mã idol hoặc số card")]
    [InlineData("NOCARD", "thiếu mã idol hoặc số card")]
    public void Needs_config_for_unknown_unconfirmed_or_incomplete_skus(string skuId, string expectedText)
    {
        var result = Classify(Order("O1", 0, [(skuId, 1)]));

        Assert.Equal(OrderCategory.NeedsConfig, result.Category);
        Assert.Contains(result.Reasons, reason => reason.StartsWith($"SKU {skuId} ", StringComparison.Ordinal) && reason.Contains(expectedText, StringComparison.Ordinal));
    }

    [Fact]
    public void Needs_config_has_priority_over_manual()
    {
        var result = Classify(Order("O1", 0, [("IVAN2M", 1), ("UNCONF", 1)]));

        Assert.Equal(OrderCategory.NeedsConfig, result.Category);
    }

    [Fact]
    public void Manual_blocks_whole_order_even_with_auto_line()
    {
        var order = Order("O1", 0, [("IVAN20", 1)]) with
        {
            Lines =
            [
                Line("O1", "IVAN20", 1),
                Line("O1", "IVAN2M", 2, variation: "20 CARD, 2 MẶT (MẶT ẢNH + CHỮ KÝ)"),
            ],
        };

        var result = Classify(order);

        Assert.Equal(OrderCategory.Manual, result.Category);
        Assert.Contains("Card thủ công: 20 CARD, 2 MẶT (MẶT ẢNH + CHỮ KÝ)", result.Reasons);
    }

    [Fact]
    public void Manual_has_priority_over_missing_file()
    {
        var result = Classify(Order("O1", 0, [("CODY30", 1), ("IVAN2M", 1)]));

        Assert.Equal(OrderCategory.Manual, result.Category);
    }

    [Fact]
    public void Missing_file_names_idol_and_page_count()
    {
        var result = Classify(Order("O1", 0, [("CODY30", 1)]));

        Assert.Equal(OrderCategory.MissingFile, result.Category);
        Assert.Contains("Thiếu file kho CODY - 4 PAGE", result.Reasons);
    }

    [Fact]
    public void Missing_file_ignores_invalid_assets_and_other_page_counts()
    {
        var assets = new[] { Asset("CODY", 4, 14, isValid: false), Asset("CODY", 3, 14) };

        var result = new OrderClassifier().Classify(Order("O1", 0, [("CODY30", 1)]), DefaultSkus, assets, Defaults);

        Assert.Equal(OrderCategory.MissingFile, result.Category);
    }

    [Fact]
    public void Idol_matching_uses_idol_key()
    {
        var skus = Skus(Auto("SONK", "SƠN.K", 20));
        var assets = new[] { Asset("SON K", 3, 14) };

        var result = new OrderClassifier().Classify(Order("O1", 0, [("SONK", 1)]), skus, assets, Defaults);

        Assert.Equal(OrderCategory.Ready, result.Category);
    }
}
