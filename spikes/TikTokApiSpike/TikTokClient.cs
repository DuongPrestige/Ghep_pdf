using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace TikTokApiSpike;

public sealed class TikTokClient(HttpClient http, string appKey, string appSecret)
{
    public const string ApiBase = "https://open-api.tiktokglobalshop.com";
    public const string AuthBase = "https://auth.tiktok-shops.com";

    public Task<JsonNode> GetTokenAsync(string authCode) =>
        GetAuthAsync("/api/v2/token/get", new()
        {
            ["app_key"] = appKey,
            ["app_secret"] = appSecret,
            ["auth_code"] = authCode,
            ["grant_type"] = "authorized_code",
        });

    public Task<JsonNode> RefreshTokenAsync(string refreshToken) =>
        GetAuthAsync("/api/v2/token/refresh", new()
        {
            ["app_key"] = appKey,
            ["app_secret"] = appSecret,
            ["refresh_token"] = refreshToken,
            ["grant_type"] = "refresh_token",
        });

    /// <summary>Calls a signed Open API endpoint. Returns the full response envelope.</summary>
    public async Task<JsonNode> CallAsync(
        HttpMethod method,
        string path,
        string accessToken,
        string? shopCipher,
        Dictionary<string, string>? query = null,
        string? body = null)
    {
        var parameters = new Dictionary<string, string>(query ?? [], StringComparer.Ordinal)
        {
            ["app_key"] = appKey,
            ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
        };

        if (!string.IsNullOrEmpty(shopCipher))
        {
            parameters["shop_cipher"] = shopCipher;
        }

        var signedBody = method == HttpMethod.Get ? null : body ?? "{}";
        parameters["sign"] = RequestSigner.Sign(appSecret, path, parameters, signedBody);

        using var request = new HttpRequestMessage(method, ApiBase + path + "?" + BuildQuery(parameters));
        request.Headers.Add("x-tts-access-token", accessToken);
        if (signedBody is not null)
        {
            request.Content = new StringContent(signedBody, Encoding.UTF8);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }

        return await SendAsync(request);
    }

    private async Task<JsonNode> GetAuthAsync(string path, Dictionary<string, string> query)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, AuthBase + path + "?" + BuildQuery(query));
        return await SendAsync(request);
    }

    private async Task<JsonNode> SendAsync(HttpRequestMessage request)
    {
        using var response = await http.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(text);
        }
        catch (System.Text.Json.JsonException)
        {
            throw new InvalidOperationException($"HTTP {(int)response.StatusCode}: non-JSON response ({text.Length} chars).");
        }

        if (node is null)
        {
            throw new InvalidOperationException($"HTTP {(int)response.StatusCode}: empty response.");
        }

        node["_http_status"] = (int)response.StatusCode;
        return node;
    }

    private static string BuildQuery(Dictionary<string, string> parameters) =>
        string.Join("&", parameters.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));
}
