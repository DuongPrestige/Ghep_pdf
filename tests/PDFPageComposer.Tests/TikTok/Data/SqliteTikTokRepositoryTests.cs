using System.IO;
using PDFPageComposer.App.TikTok.Data;
using PDFPageComposer.App.TikTok.Models;
using static PDFPageComposer.Tests.TikTok.Data.TikTokDatabaseFixture;

namespace PDFPageComposer.Tests.TikTok.Data;

public sealed class SqliteTikTokRepositoryTests
{
    [Fact]
    public void Default_path_is_under_local_app_data()
    {
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PDFPageComposer",
            "tiktok.db");

        Assert.Equal(expected, TikTokDatabasePath.Default);
    }

    [Fact]
    public async Task InitializeAsync_creates_folder_and_is_idempotent()
    {
        using var fixture = await CreateAsync(initialize: false);

        await fixture.Repository.InitializeAsync(CancellationToken.None);
        var shop = await fixture.Repository.AddShopAsync("Shop A", CancellationToken.None);
        await fixture.Repository.InitializeAsync(CancellationToken.None);

        Assert.True(File.Exists(fixture.DatabasePath));
        Assert.Equal(6, await fixture.CountAsync("SchemaVersion"));
        Assert.Equal([shop], await fixture.Repository.GetShopsAsync(CancellationToken.None));
        foreach (var table in new[] { "Shop", "Sku", "IdolAlias", "PdfAsset", "ImportBatch", "\"Order\"", "OrderLine", "PrintBatch", "PrintBatchItem" })
        {
            Assert.Equal(table == "Shop" ? 1 : 0, await fixture.CountAsync(table));
        }
    }

    [Fact]
    public async Task Schema_has_no_recipient_phone_or_address_columns()
    {
        using var fixture = await CreateAsync();

        var columns = await fixture.GetColumnsAsync("Order");

        Assert.Contains("NoteReviewedHash", columns);
        Assert.DoesNotContain(columns, column =>
            column.Contains("Recipient", StringComparison.OrdinalIgnoreCase)
            || column.Contains("Phone", StringComparison.OrdinalIgnoreCase)
            || column.Contains("Address", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Shops_are_trimmed_unique_ordered_and_renamable()
    {
        using var fixture = await CreateAsync();
        var repository = fixture.Repository;

        var beta = await repository.AddShopAsync("  Shop Bê  ", CancellationToken.None);
        var alpha = await repository.AddShopAsync("Shop An", CancellationToken.None);

        Assert.Equal("Shop Bê", beta.Name);
        Assert.Equal([alpha, beta], await repository.GetShopsAsync(CancellationToken.None));

        var duplicate = await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.AddShopAsync("shop an ", CancellationToken.None));
        Assert.Contains("đã tồn tại", duplicate.Message);
        await Assert.ThrowsAsync<ArgumentException>(() => repository.AddShopAsync("   ", CancellationToken.None));

        await repository.RenameShopAsync(beta.ShopId, " Shop Cờ ", CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.RenameShopAsync(beta.ShopId, "Shop An", CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.RenameShopAsync(999, "Shop Mới", CancellationToken.None));

        Assert.Equal(
            [alpha, beta with { Name = "Shop Cờ" }],
            await repository.GetShopsAsync(CancellationToken.None));
    }

    [Fact]
    public async Task SaveSkusAsync_round_trips_and_upserts()
    {
        using var fixture = await CreateAsync();
        var repository = fixture.Repository;
        var first = Sku("111") with { FirstSeenAt = new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero) };
        var second = Sku("222", SkuProcessType.Ignore, idol: null) with { CardCount = null };

        await repository.SaveSkusAsync([first, second], CancellationToken.None);
        var updated = first with
        {
            ProcessType = SkuProcessType.Manual,
            ConfigStatus = SkuConfigStatus.Confirmed,
            Idol = "HIEUTHUHAI",
            UpdatedAt = VnTime(16, 10, 5),
        };
        await repository.SaveSkusAsync([updated], CancellationToken.None);

        var skus = await repository.GetSkusAsync(CancellationToken.None);
        Assert.Equal([updated, second], skus);
        Assert.Equal(TimeSpan.Zero, skus[0].FirstSeenAt.Offset);
        Assert.Equal(VietnamOffset, skus[0].UpdatedAt.Offset);
    }

    [Fact]
    public async Task Idol_aliases_upsert_and_delete()
    {
        using var fixture = await CreateAsync();
        var repository = fixture.Repository;

        await repository.SaveIdolAliasAsync(new IdolAlias("HIEUTHUHAI", "HIEUTHUHAI"), CancellationToken.None);
        await repository.SaveIdolAliasAsync(new IdolAlias("HTH", "HIEU"), CancellationToken.None);
        await repository.SaveIdolAliasAsync(new IdolAlias("HTH", "HIEUTHUHAI"), CancellationToken.None);

        Assert.Equal(
            [new IdolAlias("HIEUTHUHAI", "HIEUTHUHAI"), new IdolAlias("HTH", "HIEUTHUHAI")],
            await repository.GetIdolAliasesAsync(CancellationToken.None));

        await repository.DeleteIdolAliasAsync("HTH", CancellationToken.None);
        await repository.DeleteIdolAliasAsync("NOT-THERE", CancellationToken.None);

        Assert.Equal([new IdolAlias("HIEUTHUHAI", "HIEUTHUHAI")], await repository.GetIdolAliasesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ReplacePdfAssetsAsync_replaces_whole_table_and_round_trips()
    {
        using var fixture = await CreateAsync();
        var repository = fixture.Repository;
        var lastWrite = new DateTime(2026, 10, 15, 3, 4, 5, 678, DateTimeKind.Utc).AddTicks(9);
        var valid = new PdfAsset(@"D:\Thư viện\IVAN - 4 PAGE - 15.10.pdf", "IVAN", 4, new DateOnly(2026, 10, 15), 4, lastWrite, true, null);
        var invalid = new PdfAsset(@"D:\Thư viện\sai tên.pdf", "", 0, new DateOnly(2026, 1, 1), null, lastWrite, false, "Tên file không đúng mẫu.");

        await repository.ReplacePdfAssetsAsync([valid, new PdfAsset(@"D:\old.pdf", "OLD", 1, new DateOnly(2025, 1, 2), 1, lastWrite, true, null)], CancellationToken.None);
        await repository.ReplacePdfAssetsAsync([valid, invalid], CancellationToken.None);

        var assets = await repository.GetPdfAssetsAsync(CancellationToken.None);
        Assert.Equal([valid, invalid], assets);
        Assert.Equal(DateTimeKind.Utc, assets[0].LastWriteTimeUtc.Kind);
        Assert.Equal(lastWrite.Ticks, assets[0].LastWriteTimeUtc.Ticks);
    }
}
