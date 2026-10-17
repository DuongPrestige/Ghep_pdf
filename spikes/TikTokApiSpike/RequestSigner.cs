using System.Security.Cryptography;
using System.Text;

namespace TikTokApiSpike;

/// <summary>
/// TikTok Shop Open API v202309 signature:
/// hex(HMAC-SHA256(secret, secret + path + sorted(key+value) + body + secret)).
/// </summary>
public static class RequestSigner
{
    private static readonly HashSet<string> ExcludedKeys = new(StringComparer.Ordinal) { "sign", "access_token" };

    public static string Sign(string appSecret, string path, IReadOnlyDictionary<string, string> query, string? body)
    {
        var builder = new StringBuilder();
        builder.Append(appSecret);
        builder.Append(path);

        foreach (var pair in query.Where(p => !ExcludedKeys.Contains(p.Key)).OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            builder.Append(pair.Key).Append(pair.Value);
        }

        if (!string.IsNullOrEmpty(body))
        {
            builder.Append(body);
        }

        builder.Append(appSecret);

        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(appSecret), Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexStringLower(hash);
    }
}
