using PDFPageComposer.App.Interfaces;
using PDFPageComposer.App.TikTok;
using PDFPageComposer.App.TikTok.Export;

namespace PDFPageComposer.App.ViewModels.TikTok;

/// <summary>Everything the TikTok orders window needs, resolved once from DI (or built from fakes in tests).</summary>
public sealed record TikTokServices(
    ITikTokRepository Repository,
    IAppSettingsService SettingsService,
    TimeProvider TimeProvider,
    IOrderImportService ImportService,
    Func<string, IOrderSource> OrderSourceFactory,
    ISkuConfigSuggester Suggester,
    IPdfLibraryScanner LibraryScanner,
    IOrderClassifier Classifier,
    IPrintPlanBuilder PlanBuilder,
    IPrintBatchExporter Exporter,
    IFoxitLauncherService FoxitLauncher,
    IFileDialogService FileDialogs,
    IClipboardService Clipboard,
    IOrderListExportService OrderListExporter,
    IPdfRenderService? PageRenderer = null);
