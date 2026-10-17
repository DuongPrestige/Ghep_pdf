using System.Text.Json.Nodes;

namespace TikTokApiSpike;

/// <summary>Removes buyer personal data before a response is written to disk.</summary>
public static class Redactor
{
    private static readonly HashSet<string> RedactedObjects = new(StringComparer.OrdinalIgnoreCase)
    {
        "recipient_address",
    };

    private static readonly string[] RedactedKeyFragments =
    [
        "email", "phone", "user_id", "cpf", "buyer_nickname", "first_name", "last_name", "full_address",
        "address_detail", "postal_code", "access_token", "refresh_token",
    ];

    private static readonly HashSet<string> FreeTextKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "buyer_message", "seller_note", "note", "remark",
    };

    public static JsonNode? Redact(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var key in obj.Select(p => p.Key).ToList())
                {
                    var value = obj[key];
                    if (RedactedObjects.Contains(key))
                    {
                        obj[key] = $"<redacted {value?.GetValueKind()}>";
                    }
                    else if (RedactedKeyFragments.Any(f => key.Contains(f, StringComparison.OrdinalIgnoreCase)))
                    {
                        obj[key] = "<redacted>";
                    }
                    else if (FreeTextKeys.Contains(key) && value is JsonValue)
                    {
                        var text = value.ToString();
                        obj[key] = text.Length == 0 ? "" : $"<redacted text, {text.Length} chars>";
                    }
                    else
                    {
                        Redact(value);
                    }
                }

                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    Redact(item);
                }

                break;
        }

        return node;
    }
}
