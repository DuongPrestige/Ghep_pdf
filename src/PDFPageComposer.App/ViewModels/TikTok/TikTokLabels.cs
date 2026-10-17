using PDFPageComposer.App.TikTok.Models;

namespace PDFPageComposer.App.ViewModels.TikTok;

/// <summary>A value with its Vietnamese label, used by combo boxes.</summary>
public sealed record TikTokOption<T>(T Value, string Label)
{
    public override string ToString() => Label;
}

/// <summary>Vietnamese labels of the TikTok enums.</summary>
public static class TikTokLabels
{
    /// <summary>Display order of the category chips.</summary>
    public static IReadOnlyList<OrderCategory> CategoryDisplayOrder { get; } =
    [
        OrderCategory.Ready,
        OrderCategory.MissingFile,
        OrderCategory.NeedsConfig,
        OrderCategory.Manual,
        OrderCategory.HasNote,
        OrderCategory.Printed,
        OrderCategory.NoCardNeeded,
    ];

    public static IReadOnlyList<TikTokOption<SeparatorMode>> SeparatorModeOptions { get; } =
    [
        new(SeparatorMode.None, Of(SeparatorMode.None)),
        new(SeparatorMode.ComplexOnly, Of(SeparatorMode.ComplexOnly)),
        new(SeparatorMode.All, Of(SeparatorMode.All)),
    ];

    public static IReadOnlyList<TikTokOption<SkuProcessType>> ProcessTypeOptions { get; } =
    [
        new(SkuProcessType.Auto, Of(SkuProcessType.Auto)),
        new(SkuProcessType.Manual, Of(SkuProcessType.Manual)),
        new(SkuProcessType.Ignore, Of(SkuProcessType.Ignore)),
    ];

    public static string Of(OrderCategory category) => category switch
    {
        OrderCategory.Printed => "Đã tạo file in",
        OrderCategory.NoCardNeeded => "Không cần in card",
        OrderCategory.HasNote => "Có ghi chú",
        OrderCategory.NeedsConfig => "Cần cấu hình",
        OrderCategory.Manual => "Thủ công",
        OrderCategory.MissingFile => "Thiếu file",
        OrderCategory.Ready => "Sẵn sàng",
        _ => category.ToString(),
    };

    public static string Of(SeparatorMode mode) => mode switch
    {
        SeparatorMode.None => "Không chèn",
        SeparatorMode.ComplexOnly => "Chỉ đơn phức tạp",
        SeparatorMode.All => "Mọi đơn",
        _ => mode.ToString(),
    };

    public static string Of(SkuProcessType processType) => processType switch
    {
        SkuProcessType.Auto => "Ghép tự động",
        SkuProcessType.Manual => "Thủ công",
        SkuProcessType.Ignore => "Bỏ qua",
        _ => processType.ToString(),
    };

    public static string Of(SkuConfigStatus status) => status switch
    {
        SkuConfigStatus.Confirmed => "Đã xác nhận",
        SkuConfigStatus.Unconfigured => "Chưa cấu hình",
        _ => status.ToString(),
    };

    public static string Of(ImportMode mode) => mode switch
    {
        ImportMode.Full => "File đầy đủ",
        ImportMode.Partial => "Bổ sung",
        _ => mode.ToString(),
    };

    public static string FormatImportReport(ImportReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return $"{report.Inserted} đơn mới, {report.Updated} cập nhật, {report.AlreadyPrinted} đã in trước đó, " +
               $"{report.Skipped} dòng bỏ qua, {report.Deactivated} đơn ẩn, {report.NewSkus} SKU mới";
    }

    public static string FormatDateTime(DateTimeOffset value) =>
        value.ToString("dd/MM/yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture);
}
