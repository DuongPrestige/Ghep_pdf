using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using PDFPageComposer.App.TikTok;
using PDFPageComposer.App.TikTok.Models;

namespace PDFPageComposer.App.ViewModels.TikTok;

/// <summary>One editable SKU in the "Cấu hình SKU" tab.</summary>
public sealed partial class SkuRowViewModel : ObservableObject
{
    private SkuConfig source;

    public SkuRowViewModel(SkuConfig source)
    {
        ArgumentNullException.ThrowIfNull(source);
        this.source = source;
        LoadFrom(source);
    }

    public SkuConfig Source => source;

    public string SkuId => source.SkuId;

    public string ProductName => source.ProductName;

    public string VariationName => source.VariationName;

    public bool IsConfirmed => source.ConfigStatus == SkuConfigStatus.Confirmed;

    public string StatusText => HasChanges
        ? $"{TikTokLabels.Of(source.ConfigStatus)} (đã sửa)"
        : TikTokLabels.Of(source.ConfigStatus);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanges), nameof(StatusText))]
    private SkuProcessType processType;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanges), nameof(StatusText))]
    private string? idol;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanges), nameof(StatusText))]
    private string? cardCountText;

    [ObservableProperty]
    private bool isSelected;

    public bool HasChanges =>
        ProcessType != source.ProcessType ||
        !string.Equals(NormalizedIdol, source.Idol ?? string.Empty, StringComparison.Ordinal) ||
        ParsedCardCount != source.CardCount ||
        (!string.IsNullOrWhiteSpace(CardCountText) && ParsedCardCount is null);

    private string NormalizedIdol => TextNormalizer.Normalize(Idol);

    private int? ParsedCardCount =>
        int.TryParse(CardCountText?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    /// <summary>Validates the edited values. Returns a Vietnamese error, or null when the row can be confirmed.</summary>
    public string? Validate()
    {
        if (!string.IsNullOrWhiteSpace(CardCountText) && ParsedCardCount is not > 0)
        {
            return $"SKU {SkuId}: số card phải là số nguyên dương.";
        }

        if (ProcessType == SkuProcessType.Auto)
        {
            if (NormalizedIdol.Length == 0)
            {
                return $"SKU {SkuId}: loại Ghép tự động cần mã idol.";
            }

            if (ParsedCardCount is not > 0)
            {
                return $"SKU {SkuId}: loại Ghép tự động cần số card.";
            }
        }

        return null;
    }

    /// <summary>The configuration to save when the user confirms this row.</summary>
    public SkuConfig ToConfirmed(DateTimeOffset now) => source with
    {
        ProcessType = ProcessType,
        ConfigStatus = SkuConfigStatus.Confirmed,
        Idol = NormalizedIdol.Length == 0 ? null : NormalizedIdol,
        CardCount = ParsedCardCount,
        UpdatedAt = now,
    };

    /// <summary>Replaces the editable values with a fresh suggestion (only used for unconfirmed, unedited rows).</summary>
    public void ApplySuggestion(SkuSuggestion suggestion)
    {
        ArgumentNullException.ThrowIfNull(suggestion);
        source = source with
        {
            ProcessType = suggestion.ProcessType,
            Idol = suggestion.Idol,
            CardCount = suggestion.CardCount,
        };
        LoadFrom(source);
    }

    /// <summary>Marks the row as saved with the stored configuration.</summary>
    public void AcceptSaved(SkuConfig saved)
    {
        ArgumentNullException.ThrowIfNull(saved);
        source = saved;
        LoadFrom(saved);
        IsSelected = false;
    }

    private void LoadFrom(SkuConfig config)
    {
        ProcessType = config.ProcessType;
        Idol = config.Idol;
        CardCountText = config.CardCount?.ToString(CultureInfo.InvariantCulture);
        OnPropertyChanged(nameof(IsConfirmed));
        OnPropertyChanged(nameof(HasChanges));
        OnPropertyChanged(nameof(StatusText));
    }
}
