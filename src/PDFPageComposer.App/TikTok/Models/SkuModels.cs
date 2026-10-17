namespace PDFPageComposer.App.TikTok.Models;

/// <summary>Per-SKU configuration (PRD_TIKTOK TT-03). Idol is stored normalized (TextNormalizer.Normalize).</summary>
public sealed record SkuConfig(
    string SkuId,
    string ProductName,
    string VariationName,
    SkuProcessType ProcessType,
    SkuConfigStatus ConfigStatus,
    string? Idol,
    int? CardCount,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset UpdatedAt);

public sealed record SkuSuggestion(SkuProcessType ProcessType, string? Idol, int? CardCount, int? Sides);

/// <summary>Alias is stored as TextNormalizer.IdolKey(alias); Idol is stored normalized.</summary>
public sealed record IdolAlias(string Alias, string Idol);
