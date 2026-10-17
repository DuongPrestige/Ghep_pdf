using System.Text.Json;
using PDFPageComposer.App.Interfaces;
using PDFPageComposer.App.Models;
using Serilog;

namespace PDFPageComposer.App.TikTok;

/// <summary>Loads settings.json for the TikTok module without letting a corrupt file block the whole window.</summary>
public static class TikTokSettingsLoader
{
    public const string CorruptNotice =
        "File cài đặt (settings.json) bị lỗi định dạng, đang dùng cài đặt TikTok mặc định. Hãy sửa hoặc xóa file này.";

    /// <summary>Returns the stored settings, or defaults with WasCorrupt = true when the file is not valid JSON.</summary>
    public static async Task<(AppSettings Settings, bool WasCorrupt)> LoadAsync(
        IAppSettingsService settingsService,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settingsService);
        try
        {
            return (await settingsService.LoadAsync(cancellationToken).ConfigureAwait(false), false);
        }
        catch (JsonException exception)
        {
            Log.Warning(
                "settings.json is not valid JSON ({ExceptionType}); using default TikTok settings",
                exception.GetType().FullName);
            return (new AppSettings(), true);
        }
    }

    /// <summary>
    /// Loads the latest settings before a save. A corrupt file is never overwritten with defaults, because that
    /// would wipe the user's other settings.
    /// </summary>
    /// <exception cref="InvalidOperationException">settings.json is not valid JSON.</exception>
    public static async Task<AppSettings> LoadForSaveAsync(
        IAppSettingsService settingsService,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settingsService);
        try
        {
            return await settingsService.LoadAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                "Không lưu được cài đặt vì file settings.json đang bị lỗi định dạng. Hãy sửa hoặc xóa file rồi thử lại.",
                exception);
        }
    }

    /// <summary>Copies every TikTok setting, so an edit can be saved first and applied only on success.</summary>
    public static TikTokSettings Copy(TikTokSettings source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new TikTokSettings
        {
            PdfLibraryFolder = source.PdfLibraryFolder,
            OutputFolder = source.OutputFolder,
            SellerNoteMarksPrinted = source.SellerNoteMarksPrinted,
            BlockOrdersWithNotes = source.BlockOrdersWithNotes,
            AlwaysUseNewestFiles = source.AlwaysUseNewestFiles,
            DefaultSeparatorMode = source.DefaultSeparatorMode,
            AcceptedStatuses = source.AcceptedStatuses is null ? [] : [.. source.AcceptedStatuses],
            LastImportShopId = source.LastImportShopId,
        };
    }
}
