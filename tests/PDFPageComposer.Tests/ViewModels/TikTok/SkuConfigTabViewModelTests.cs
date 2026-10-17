using PDFPageComposer.App.TikTok.Models;
using PDFPageComposer.App.ViewModels.TikTok;

namespace PDFPageComposer.Tests.ViewModels.TikTok;

public sealed class SkuConfigTabViewModelTests : IDisposable
{
    private readonly TikTokUiHarness harness = new();

    public void Dispose() => harness.Dispose();

    private async Task<TikTokOrdersViewModel> LoadWithSkusAsync(params SkuConfig[] skus)
    {
        await harness.AddShopAsync();
        await harness.Repository.SaveSkusAsync(skus, CancellationToken.None);
        var viewModel = harness.CreateViewModel();
        await viewModel.LoadCommand.ExecuteAsync(null);
        return viewModel;
    }

    [Fact]
    public async Task Unconfigured_filter_is_on_by_default_and_search_narrows_rows()
    {
        var viewModel = await LoadWithSkusAsync(
            TikTokUiHarness.Sku("A", SkuProcessType.Auto, "CODY", 30, SkuConfigStatus.Unconfigured),
            TikTokUiHarness.Sku("B", SkuProcessType.Ignore, null, null, SkuConfigStatus.Unconfigured, product: "[MÓC KHÓA] Spotify"),
            TikTokUiHarness.Sku("C", SkuProcessType.Auto, "IVAN", 20));
        var skus = viewModel.Skus;

        Assert.Equal(["A", "B"], skus.Rows.Select(row => row.SkuId));
        Assert.Equal("3 SKU, 2 chưa cấu hình", skus.Summary);

        skus.SearchText = "moc khoa";
        Assert.Equal(["B"], skus.Rows.Select(row => row.SkuId));

        skus.SearchText = null;
        skus.ShowUnconfiguredOnly = false;
        Assert.Equal(3, skus.Rows.Count);
    }

    [Fact]
    public async Task Bulk_confirm_saves_valid_rows_and_reports_invalid_ones()
    {
        var viewModel = await LoadWithSkusAsync(
            TikTokUiHarness.Sku("A", SkuProcessType.Auto, "CODY", 30, SkuConfigStatus.Unconfigured),
            TikTokUiHarness.Sku("B", SkuProcessType.Auto, null, 30, SkuConfigStatus.Unconfigured),
            TikTokUiHarness.Sku("C", SkuProcessType.Ignore, null, null, SkuConfigStatus.Unconfigured));
        var skus = viewModel.Skus;
        skus.SelectAllVisibleCommand.Execute(null);

        await skus.ConfirmSelectedCommand.ExecuteAsync(null);

        var stored = (await harness.Repository.GetSkusAsync(CancellationToken.None)).ToDictionary(sku => sku.SkuId);
        Assert.Equal(SkuConfigStatus.Confirmed, stored["A"].ConfigStatus);
        Assert.Equal(SkuConfigStatus.Confirmed, stored["C"].ConfigStatus);
        Assert.Equal(SkuConfigStatus.Unconfigured, stored["B"].ConfigStatus);
        Assert.Equal(TikTokUiHarness.LocalNow, stored["A"].UpdatedAt);
        Assert.Contains("SKU B: loại Ghép tự động cần mã idol.", viewModel.Workspace.ErrorText);
        Assert.Equal(["B"], skus.Rows.Select(row => row.SkuId));
        Assert.Equal("Đã xác nhận 2 SKU.", viewModel.Workspace.StatusText);
    }

    [Fact]
    public async Task Inline_edit_then_confirm_normalizes_idol_and_reclassifies_orders()
    {
        var shopId = await harness.AddShopAsync();
        await harness.Repository.SaveSkusAsync(
            [TikTokUiHarness.Sku("A", SkuProcessType.Manual, null, null, SkuConfigStatus.Unconfigured)],
            CancellationToken.None);
        await harness.Repository.ReplacePdfAssetsAsync(TikTokUiHarness.ExampleAssets(), CancellationToken.None);
        await harness.SeedOrdersAsync(shopId, TikTokUiHarness.Order("O1", shopId, 1, [("A", 1)]));
        var viewModel = harness.CreateViewModel();
        await viewModel.LoadCommand.ExecuteAsync(null);
        Assert.Equal(OrderCategory.NeedsConfig, viewModel.Orders.AllRows.Single().Category);
        var row = viewModel.Skus.Rows.Single();

        row.ProcessType = SkuProcessType.Auto;
        row.Idol = "cody";
        row.CardCountText = "30";
        Assert.True(row.HasChanges);
        await viewModel.Skus.ConfirmRowCommand.ExecuteAsync(row);

        var stored = Assert.Single(await harness.Repository.GetSkusAsync(CancellationToken.None));
        Assert.Equal("CODY", stored.Idol);
        Assert.Equal(30, stored.CardCount);
        Assert.Equal(SkuProcessType.Auto, stored.ProcessType);
        Assert.Equal(OrderCategory.Ready, viewModel.Orders.AllRows.Single().Category);
    }

    [Fact]
    public async Task Invalid_card_count_is_not_saved()
    {
        var viewModel = await LoadWithSkusAsync(
            TikTokUiHarness.Sku("A", SkuProcessType.Manual, null, null, SkuConfigStatus.Unconfigured));
        var row = viewModel.Skus.Rows.Single();
        row.CardCountText = "abc";

        await viewModel.Skus.ConfirmRowCommand.ExecuteAsync(row);

        Assert.Equal("SKU A: số card phải là số nguyên dương.", viewModel.Workspace.ErrorText);
        Assert.Equal(SkuConfigStatus.Unconfigured, Assert.Single(await harness.Repository.GetSkusAsync(CancellationToken.None)).ConfigStatus);
    }

    [Fact]
    public async Task Ignore_product_confirms_every_sku_of_the_same_product()
    {
        var viewModel = await LoadWithSkusAsync(
            TikTokUiHarness.Sku("K1", SkuProcessType.Manual, null, null, SkuConfigStatus.Unconfigured, product: "[MÓC KHÓA] Spotify"),
            TikTokUiHarness.Sku("K2", SkuProcessType.Auto, "IVAN", 9, SkuConfigStatus.Unconfigured, product: "[móc khóa]  spotify"),
            TikTokUiHarness.Sku("X", SkuProcessType.Auto, "CODY", 30, SkuConfigStatus.Unconfigured));

        await viewModel.Skus.IgnoreProductCommand.ExecuteAsync(viewModel.Skus.Rows.First(row => row.SkuId == "K1"));

        var stored = (await harness.Repository.GetSkusAsync(CancellationToken.None)).ToDictionary(sku => sku.SkuId);
        Assert.All(new[] { stored["K1"], stored["K2"] }, sku =>
        {
            Assert.Equal(SkuProcessType.Ignore, sku.ProcessType);
            Assert.Equal(SkuConfigStatus.Confirmed, sku.ConfigStatus);
        });
        Assert.Equal(SkuConfigStatus.Unconfigured, stored["X"].ConfigStatus);
    }

    [Fact]
    public async Task Add_alias_saves_the_key_and_refreshes_unedited_suggestions()
    {
        var viewModel = await LoadWithSkusAsync(
            TikTokUiHarness.Sku(
                "S1",
                SkuProcessType.Auto,
                "CONG B",
                30,
                SkuConfigStatus.Unconfigured,
                product: "[CARD BO GÓC CÔNG B] - ẢNH MỚI",
                variation: "30 CARD, 1 Mặt"));
        var skus = viewModel.Skus;
        skus.NewAlias = "Công B";
        skus.NewAliasIdol = "congb";

        await skus.AddAliasCommand.ExecuteAsync(null);

        var alias = Assert.Single(await harness.Repository.GetIdolAliasesAsync(CancellationToken.None));
        Assert.Equal(new IdolAlias("CONGB", "CONGB"), alias);
        Assert.Equal(alias, Assert.Single(skus.Aliases));
        Assert.Null(skus.NewAlias);
        Assert.Equal("CONGB", skus.AllRows.Single().Idol);

        await skus.DeleteAliasCommand.ExecuteAsync(alias);

        Assert.Empty(await harness.Repository.GetIdolAliasesAsync(CancellationToken.None));
        Assert.Empty(skus.Aliases);
    }

    [Fact]
    public async Task Add_alias_requires_both_values()
    {
        var viewModel = await LoadWithSkusAsync();
        viewModel.Skus.NewAlias = "Công B";

        await viewModel.Skus.AddAliasCommand.ExecuteAsync(null);

        Assert.Equal("Nhập cả cách viết (alias) và mã idol.", viewModel.Workspace.ErrorText);
    }
}
