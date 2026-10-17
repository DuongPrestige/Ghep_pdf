using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Web;
using TikTokApiSpike;

Console.OutputEncoding = Encoding.UTF8;

var command = args.FirstOrDefault()?.ToLowerInvariant();
var options = ParseOptions(args.Skip(1).ToArray());
var state = SpikeStateStore.Load();
var outputDirectory = ResolveOutputDirectory();
using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

try
{
    switch (command)
    {
        case "setup":
            Setup();
            break;
        case "status":
            PrintStatus();
            break;
        case "auth":
            await AuthAsync(options.Positional.FirstOrDefault() ?? throw new ArgumentException("Thiếu auth code. Dùng: auth <code hoặc URL redirect>"));
            break;
        case "refresh":
            await RefreshAsync();
            break;
        case "shops":
            await ShopsAsync();
            break;
        case "orders":
            await OrdersAsync();
            break;
        case "products":
            await ProductsAsync();
            break;
        case "raw":
            await RawAsync();
            break;
        case "selftest":
            SelfTest();
            break;
        case "reset":
            SpikeStateStore.Delete();
            Console.WriteLine("Đã xóa state đã lưu.");
            break;
        default:
            PrintHelp();
            break;
    }
}
catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or HttpRequestException or TaskCanceledException)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"Lỗi: {ex.Message}");
    Console.ResetColor();
    Environment.ExitCode = 1;
}

void Setup()
{
    Console.Write("App key: ");
    var appKey = Console.ReadLine()?.Trim();
    Console.Write("App secret (ẩn khi gõ): ");
    var appSecret = ReadMasked();
    if (string.IsNullOrWhiteSpace(appKey) || string.IsNullOrWhiteSpace(appSecret))
    {
        throw new ArgumentException("App key và secret không được trống.");
    }

    state.AppKey = appKey;
    state.AppSecret = appSecret;
    SpikeStateStore.Save(state);
    Console.WriteLine("Đã lưu (mã hóa DPAPI). Bước tiếp: mở link cấp quyền shop trong Partner Center rồi chạy `auth <code>`.");
}

void PrintStatus()
{
    Console.WriteLine($"App key: {(state.AppKey is null ? "(chưa có)" : state.AppKey)}");
    Console.WriteLine($"App secret: {(state.AppSecret is null ? "(chưa có)" : "đã lưu")}");
    Console.WriteLine($"Output: {outputDirectory}");
    foreach (var auth in state.Authorizations)
    {
        Console.WriteLine($"- Seller '{auth.SellerName}' open_id={auth.OpenId}");
        Console.WriteLine($"  access hết hạn: {FormatExpiry(auth.AccessTokenExpireAt)}, refresh hết hạn: {FormatExpiry(auth.RefreshTokenExpireAt)}");
        Console.WriteLine($"  scopes: {auth.GrantedScopes}");
        foreach (var shop in auth.Shops)
        {
            Console.WriteLine($"  shop {shop.Id} '{shop.Name}' region={shop.Region}");
        }
    }
}

async Task AuthAsync(string codeOrUrl)
{
    var client = CreateClient();
    var code = codeOrUrl;
    if (Uri.TryCreate(codeOrUrl, UriKind.Absolute, out var uri))
    {
        code = HttpUtility.ParseQueryString(uri.Query)["code"] ?? throw new ArgumentException("URL không có tham số code.");
    }

    var response = await client.GetTokenAsync(code);
    var data = EnsureSuccess(response, "token/get");
    var auth = ApplyToken(data, null);
    Save("auth-token-get", response);
    SpikeStateStore.Save(state);
    Console.WriteLine($"OK. Seller '{auth.SellerName}', access hết hạn {FormatExpiry(auth.AccessTokenExpireAt)}, refresh hết hạn {FormatExpiry(auth.RefreshTokenExpireAt)}.");
    Console.WriteLine("Bước tiếp: chạy `shops`.");
}

async Task RefreshAsync()
{
    var client = CreateClient();
    foreach (var auth in state.Authorizations)
    {
        var response = await client.RefreshTokenAsync(auth.RefreshToken);
        var data = EnsureSuccess(response, "token/refresh");
        ApplyToken(data, auth);
        Save($"auth-token-refresh-{auth.OpenId}", response);
        Console.WriteLine($"Refresh OK '{auth.SellerName}': access hết hạn {FormatExpiry(auth.AccessTokenExpireAt)}.");
    }

    SpikeStateStore.Save(state);
}

async Task ShopsAsync()
{
    var client = CreateClient();
    foreach (var auth in state.Authorizations)
    {
        var response = await client.CallAsync(HttpMethod.Get, "/authorization/202309/shops", auth.AccessToken, null);
        var data = EnsureSuccess(response, "shops");
        Save($"shops-{auth.OpenId}", response);
        auth.Shops = (data["shops"]?.AsArray() ?? [])
            .Select(s => new ShopInfo
            {
                Id = s?["id"]?.ToString() ?? "",
                Name = s?["name"]?.ToString() ?? "",
                Region = s?["region"]?.ToString() ?? "",
                Cipher = s?["cipher"]?.ToString() ?? "",
            })
            .ToList();
        foreach (var shop in auth.Shops)
        {
            Console.WriteLine($"Shop {shop.Id} '{shop.Name}' region={shop.Region}");
        }
    }

    SpikeStateStore.Save(state);
}

async Task OrdersAsync()
{
    var client = CreateClient();
    var status = options.Get("status") ?? "AWAITING_SHIPMENT";
    var max = int.Parse(options.Get("max") ?? "100");

    foreach (var (auth, shop) in SelectedShops())
    {
        Console.WriteLine($"== Shop '{shop.Name}' ({shop.Id}) — đơn {status}");
        var orderIds = new List<string>();
        string? pageToken = null;
        var page = 0;
        do
        {
            var query = new Dictionary<string, string>
            {
                ["page_size"] = "50",
                ["sort_field"] = "create_time",
                ["sort_order"] = "ASC",
            };
            if (pageToken is not null)
            {
                query["page_token"] = pageToken;
            }

            var body = new JsonObject { ["order_status"] = status }.ToJsonString();
            var response = await client.CallAsync(HttpMethod.Post, "/order/202309/orders/search", auth.AccessToken, shop.Cipher, query, body);
            var data = EnsureSuccess(response, "orders/search");
            Save($"orders-search-{shop.Id}-p{++page}", response);

            Console.WriteLine($"  page {page}: total_count={data["total_count"]}, nhận {data["orders"]?.AsArray().Count ?? 0} đơn");
            orderIds.AddRange((data["orders"]?.AsArray() ?? []).Select(o => o?["id"]?.ToString()).OfType<string>());
            pageToken = data["next_page_token"]?.ToString();
        }
        while (!string.IsNullOrEmpty(pageToken) && orderIds.Count < max);

        var details = new List<JsonNode>();
        foreach (var chunk in orderIds.Take(max).Chunk(50))
        {
            var query = new Dictionary<string, string> { ["ids"] = string.Join(",", chunk) };
            var response = await client.CallAsync(HttpMethod.Get, "/order/202309/orders", auth.AccessToken, shop.Cipher, query);
            var data = EnsureSuccess(response, "orders detail");
            Save($"orders-detail-{shop.Id}-{details.Count}", response);
            details.AddRange((data["orders"]?.AsArray() ?? []).OfType<JsonNode>());
        }

        Analysis.AnalyzeOrders(details, orderIds.Count);
    }
}

async Task ProductsAsync()
{
    var client = CreateClient();
    var max = int.Parse(options.Get("max") ?? "200");

    foreach (var (auth, shop) in SelectedShops())
    {
        Console.WriteLine($"== Shop '{shop.Name}' ({shop.Id}) — sản phẩm");
        var products = new List<JsonNode>();
        string? pageToken = null;
        var page = 0;
        do
        {
            var query = new Dictionary<string, string> { ["page_size"] = "100" };
            if (pageToken is not null)
            {
                query["page_token"] = pageToken;
            }

            var body = new JsonObject { ["status"] = options.Get("status") ?? "ACTIVATE" }.ToJsonString();
            var response = await client.CallAsync(HttpMethod.Post, "/product/202309/products/search", auth.AccessToken, shop.Cipher, query, body);
            var data = EnsureSuccess(response, "products/search");
            Save($"products-search-{shop.Id}-p{++page}", response);

            products.AddRange((data["products"]?.AsArray() ?? []).OfType<JsonNode>());
            Console.WriteLine($"  page {page}: total_count={data["total_count"]}, nhận {products.Count}");
            pageToken = data["next_page_token"]?.ToString();
        }
        while (!string.IsNullOrEmpty(pageToken) && products.Count < max);

        Analysis.AnalyzeProducts(products);
    }
}

async Task RawAsync()
{
    if (options.Positional.Count < 2)
    {
        throw new ArgumentException("Dùng: raw <GET|POST> <path> [--shop id] [--query k=v,k2=v2] [--body json]");
    }

    var client = CreateClient();
    var method = new HttpMethod(options.Positional[0].ToUpperInvariant());
    var path = options.Positional[1];
    var query = (options.Get("query") ?? "")
        .Split(',', StringSplitOptions.RemoveEmptyEntries)
        .Select(p => p.Split('=', 2))
        .ToDictionary(p => p[0], p => p.Length > 1 ? p[1] : "");

    var (auth, shop) = SelectedShops().First();
    var useCipher = options.Get("no-cipher") is null;
    var response = await client.CallAsync(method, path, auth.AccessToken, useCipher ? shop.Cipher : null, query, options.Get("body"));
    var file = Save($"raw-{path.Trim('/').Replace('/', '_')}", response);
    Console.WriteLine($"code={response["code"]} message={response["message"]} http={response["_http_status"]}");
    Console.WriteLine($"Đã lưu: {file}");
}

static void SelfTest()
{
    // Reference value computed independently with: openssl dgst -sha256 -hmac secret
    const string expected = "d5d616c13009df4bfdb0b503e2d19be89451ed77d7dfd50c2b75ddb8118a1cd2";
    var query = new Dictionary<string, string>
    {
        ["app_key"] = "abc",
        ["timestamp"] = "1700000000",
        ["shop_cipher"] = "CIPH",
        ["page_size"] = "50",
        ["access_token"] = "ignored",
        ["sign"] = "ignored",
    };
    var actual = RequestSigner.Sign("secret", "/order/202309/orders/search", query, """{"order_status":"AWAITING_SHIPMENT"}""");
    Console.WriteLine(actual == expected ? "Signer OK" : $"Signer SAI: {actual}");
    if (actual != expected)
    {
        Environment.ExitCode = 1;
    }
}

TikTokClient CreateClient()
{
    if (state.AppKey is null || state.AppSecret is null)
    {
        throw new InvalidOperationException("Chưa có app key/secret. Chạy `setup` trước.");
    }

    return new TikTokClient(http, state.AppKey, state.AppSecret);
}

IEnumerable<(SellerAuthorization Auth, ShopInfo Shop)> SelectedShops()
{
    var filter = options.Get("shop");
    var shops = state.AllShops()
        .Where(x => filter is null || x.Shop.Id == filter || x.Shop.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
        .ToList();
    if (shops.Count == 0)
    {
        throw new InvalidOperationException("Không có shop nào. Chạy `auth` rồi `shops` trước (hoặc kiểm tra --shop).");
    }

    return shops;
}

SellerAuthorization ApplyToken(JsonNode data, SellerAuthorization? existing)
{
    var openId = data["open_id"]?.ToString() ?? "";
    var auth = existing
        ?? state.Authorizations.FirstOrDefault(a => a.OpenId == openId && openId != "")
        ?? new SellerAuthorization();
    if (!state.Authorizations.Contains(auth))
    {
        state.Authorizations.Add(auth);
    }

    auth.OpenId = openId;
    auth.SellerName = data["seller_name"]?.ToString() ?? auth.SellerName;
    auth.AccessToken = data["access_token"]?.ToString() ?? throw new InvalidOperationException("Response không có access_token.");
    auth.AccessTokenExpireAt = data["access_token_expire_in"]?.GetValue<long>() ?? 0;
    auth.RefreshToken = data["refresh_token"]?.ToString() ?? auth.RefreshToken;
    auth.RefreshTokenExpireAt = data["refresh_token_expire_in"]?.GetValue<long>() ?? 0;
    auth.GrantedScopes = data["granted_scopes"]?.ToJsonString();
    return auth;
}

JsonNode EnsureSuccess(JsonNode response, string label)
{
    var code = response["code"]?.GetValue<long>() ?? -1;
    if (code != 0)
    {
        var file = Save($"error-{label.Replace('/', '_')}", response);
        throw new InvalidOperationException(
            $"{label}: code={code}, message={response["message"]}, http={response["_http_status"]}, request_id={response["request_id"]}. Chi tiết: {file}");
    }

    return response["data"] ?? new JsonObject();
}

string Save(string name, JsonNode response)
{
    Directory.CreateDirectory(outputDirectory);
    var copy = Redactor.Redact(response.DeepClone());
    var file = Path.Combine(outputDirectory, $"{DateTime.Now:yyyyMMdd-HHmmss}-{name}.json");
    File.WriteAllText(file, copy!.ToJsonString(new JsonSerializerOptions
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    }));
    return file;
}

static string FormatExpiry(long value)
{
    if (value <= 0)
    {
        return "?";
    }

    // TikTok returns *_expire_in as a Unix timestamp; fall back to "seconds from now" if it is small.
    var at = value > 1_000_000_000
        ? DateTimeOffset.FromUnixTimeSeconds(value).ToLocalTime()
        : DateTimeOffset.Now.AddSeconds(value);
    return $"{at:yyyy-MM-dd HH:mm} (raw={value})";
}

static string ResolveOutputDirectory()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PDFPageComposer.slnx")))
    {
        directory = directory.Parent;
    }

    return Path.Combine(directory?.FullName ?? Directory.GetCurrentDirectory(), "artifacts", "tiktok-spike");
}

static string ReadMasked()
{
    var builder = new StringBuilder();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter)
        {
            Console.WriteLine();
            return builder.ToString().Trim();
        }

        if (key.Key == ConsoleKey.Backspace)
        {
            if (builder.Length > 0)
            {
                builder.Length--;
                Console.Write("\b \b");
            }
        }
        else if (!char.IsControl(key.KeyChar))
        {
            builder.Append(key.KeyChar);
            Console.Write('*');
        }
    }
}

static Options ParseOptions(string[] input)
{
    var result = new Options();
    for (var i = 0; i < input.Length; i++)
    {
        if (input[i].StartsWith("--", StringComparison.Ordinal))
        {
            var key = input[i][2..];
            var hasValue = i + 1 < input.Length && !input[i + 1].StartsWith("--", StringComparison.Ordinal);
            result.Named[key] = hasValue ? input[++i] : "true";
        }
        else
        {
            result.Positional.Add(input[i]);
        }
    }

    return result;
}

static void PrintHelp()
{
    Console.WriteLine("""
        TikTok Shop API spike (TASK-057)

          setup                       Nhập app key + secret (lưu mã hóa DPAPI)
          auth <code|redirect URL>    Đổi auth code lấy token (chạy 1 lần cho mỗi seller)
          refresh                     Làm mới token
          shops                       Lấy danh sách shop đã cấp quyền
          orders   [--shop X] [--status AWAITING_SHIPMENT] [--max 100]
          products [--shop X] [--status ACTIVATE] [--max 200]
          raw <GET|POST> <path> [--shop X] [--query k=v,k2=v2] [--body json] [--no-cipher]
          status                      Xem state (không in secret/token)
          reset                       Xóa state đã lưu

        JSON response (đã xóa dữ liệu người mua) được lưu ở artifacts/tiktok-spike/.
        """);
}

sealed class Options
{
    public List<string> Positional { get; } = [];

    public Dictionary<string, string> Named { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string? Get(string key) => Named.TryGetValue(key, out var value) ? value : null;
}
