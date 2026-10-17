namespace PDFPageComposer.App.TikTok.Models;

public enum SkuProcessType
{
    Auto,
    Manual,
    Ignore,
}

public enum SkuConfigStatus
{
    Unconfigured,
    Confirmed,
}

public enum ImportMode
{
    /// <summary>The file contains every awaiting-shipment order of the shop; missing orders are deactivated.</summary>
    Full,

    /// <summary>The file is a subset; no order is deactivated.</summary>
    Partial,
}

/// <summary>Order groups from PRD_TIKTOK TT-08, in evaluation priority order.</summary>
public enum OrderCategory
{
    Printed,
    NoCardNeeded,
    HasNote,
    NeedsConfig,
    Manual,
    MissingFile,
    Ready,
}

public enum PrintedSource
{
    Batch,
    SellerNote,
}

public enum SeparatorMode
{
    None,
    ComplexOnly,
    All,
}
