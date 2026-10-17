using PDFPageComposer.App.TikTok.Models;

namespace PDFPageComposer.App.Models;

public sealed class TikTokSettings
{
    public string? PdfLibraryFolder { get; set; }

    public string? OutputFolder { get; set; }

    public bool SellerNoteMarksPrinted { get; set; } = true;

    /// <summary>When false, orders with a Buyer Message or Seller Note are merged like any other order.</summary>
    public bool BlockOrdersWithNotes { get; set; } = true;

    /// <summary>Compose every set from the idol's newest files instead of looking for a file with the exact page count.</summary>
    public bool AlwaysUseNewestFiles { get; set; } = true;

    public SeparatorMode DefaultSeparatorMode { get; set; } = SeparatorMode.ComplexOnly;

    /// <summary>Order Status values (compared after TextNormalizer.Normalize) accepted as awaiting shipment.</summary>
    public List<string> AcceptedStatuses { get; set; } = ["Cần vận chuyển", "Chờ vận chuyển", "AWAITING_SHIPMENT"];

    public long? LastImportShopId { get; set; }
}
