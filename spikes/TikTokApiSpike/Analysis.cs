using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace TikTokApiSpike;

/// <summary>Prints the facts TASK-057 must confirm, computed from real responses.</summary>
public static partial class Analysis
{
    public static void AnalyzeOrders(IReadOnlyList<JsonNode> orders, int searchedCount)
    {
        Console.WriteLine($"  -- Phân tích {orders.Count}/{searchedCount} đơn chi tiết");
        if (orders.Count == 0)
        {
            return;
        }

        var lineItems = orders.SelectMany(o => o["line_items"]?.AsArray().OfType<JsonNode>() ?? []).ToList();
        Console.WriteLine($"  Trường cấp đơn: {string.Join(", ", KeysOf(orders))}");
        Console.WriteLine($"  Trường line_item: {string.Join(", ", KeysOf(lineItems))}");

        var noteLikePaths = orders.SelectMany(o => FindPaths(o, "")).Where(p => NoteLike().IsMatch(p)).Distinct().Order().ToList();
        Console.WriteLine($"  Trường giống note/tag/memo/message: {(noteLikePaths.Count == 0 ? "(không có)" : string.Join(", ", noteLikePaths))}");

        var hasQuantityField = lineItems.Any(li => li is JsonObject obj && obj.Any(p => p.Key.Contains("quantity", StringComparison.OrdinalIgnoreCase)));
        var ordersWithRepeatedSku = orders.Count(o =>
        {
            var skuIds = o["line_items"]?.AsArray().Select(li => li?["sku_id"]?.ToString()).ToList() ?? [];
            return skuIds.Count != skuIds.Distinct().Count();
        });
        Console.WriteLine($"  line_item có trường quantity: {(hasQuantityField ? "CÓ" : "KHÔNG")}");
        Console.WriteLine($"  Số đơn có nhiều line_item trùng sku_id (=> mỗi line_item là 1 đơn vị): {ordersWithRepeatedSku}");
        Console.WriteLine($"  Phân bố số line_item/đơn: {string.Join(", ", orders.GroupBy(o => o["line_items"]?.AsArray().Count ?? 0).OrderBy(g => g.Key).Select(g => $"{g.Key} item: {g.Count()} đơn"))}");

        Console.WriteLine("  Mẫu product_name | sku_name (sku_id):");
        foreach (var item in lineItems
                     .Select(li => (Product: li["product_name"]?.ToString(), Sku: li["sku_name"]?.ToString(), Id: li["sku_id"]?.ToString()))
                     .DistinctBy(x => x.Id)
                     .Take(40))
        {
            Console.WriteLine($"    {Truncate(item.Product, 60)} | {item.Sku} ({item.Id})");
        }
    }

    public static void AnalyzeProducts(IReadOnlyList<JsonNode> products)
    {
        var skus = products.SelectMany(p => p["skus"]?.AsArray().OfType<JsonNode>() ?? []).ToList();
        Console.WriteLine($"  -- {products.Count} sản phẩm, {skus.Count} SKU");
        if (products.Count == 0)
        {
            return;
        }

        Console.WriteLine($"  Trường product: {string.Join(", ", KeysOf(products))}");
        Console.WriteLine($"  Trường sku: {string.Join(", ", KeysOf(skus))}");
        Console.WriteLine("  Mẫu title → SKU (ghép sales_attributes.value_name):");
        foreach (var product in products.Take(15))
        {
            Console.WriteLine($"    {Truncate(product["title"]?.ToString(), 70)}");
            foreach (var sku in product["skus"]?.AsArray().OfType<JsonNode>().Take(6) ?? [])
            {
                var attributes = sku["sales_attributes"]?.AsArray()
                    .Select(a => $"{a?["name"]}={a?["value_name"]}") ?? [];
                Console.WriteLine($"      {sku["id"]}: {string.Join(" | ", attributes)}");
            }
        }
    }

    private static IEnumerable<string> KeysOf(IEnumerable<JsonNode> nodes) =>
        nodes.OfType<JsonObject>().SelectMany(o => o.Select(p => p.Key)).Distinct().Order();

    private static IEnumerable<string> FindPaths(JsonNode? node, string prefix)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, value) in obj)
                {
                    var path = prefix.Length == 0 ? key : $"{prefix}.{key}";
                    yield return path;
                    foreach (var child in FindPaths(value, path))
                    {
                        yield return child;
                    }
                }

                break;
            case JsonArray array:
                foreach (var child in array.SelectMany(item => FindPaths(item, prefix + "[]")))
                {
                    yield return child;
                }

                break;
        }
    }

    private static string Truncate(string? value, int length) =>
        value is null ? "" : value.Length <= length ? value : value[..length] + "…";

    [GeneratedRegex("note|tag|memo|remark|message|label", RegexOptions.IgnoreCase)]
    private static partial Regex NoteLike();
}
