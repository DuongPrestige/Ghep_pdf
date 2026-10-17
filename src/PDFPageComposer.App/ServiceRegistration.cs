using Microsoft.Extensions.DependencyInjection;
using PDFPageComposer.App.Interfaces;
using PDFPageComposer.App.Services;
using PDFPageComposer.App.TikTok;
using PDFPageComposer.App.TikTok.Data;
using PDFPageComposer.App.TikTok.Export;
using PDFPageComposer.App.TikTok.Import;
using PDFPageComposer.App.TikTok.Matching;
using PDFPageComposer.App.ViewModels;
using PDFPageComposer.App.ViewModels.TikTok;
using PDFPageComposer.App.Views;

namespace PDFPageComposer.App;

/// <summary>The application's dependency-injection composition root, shared by App and the composition tests.</summary>
public static class ServiceRegistration
{
    /// <summary>Registers every service, view model and window of the application.</summary>
    public static IServiceCollection AddPdfPageComposer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<PdfiumLibrary>();
        services.AddSingleton<IPdfMetadataService, PdfMetadataService>();
        services.AddSingleton<IPdfRenderService, PdfRenderService>();
        services.AddSingleton<IThumbnailCacheService, ThumbnailCacheService>();
        services.AddSingleton<IThumbnailRenderQueue, ThumbnailRenderQueue>();
        services.AddSingleton<IPdfExportService, PdfExportService>();
        services.AddSingleton<IFileDialogService, FileDialogService>();
        services.AddSingleton<IAppSettingsService, AppSettingsService>();
        services.AddSingleton<IProjectPersistenceService, ProjectPersistenceService>();
        services.AddSingleton<IAutoSaveService, AutoSaveService>();
        services.AddSingleton<IProcessLauncher, ProcessLauncher>();
        services.AddSingleton<IFoxitLauncherService, FoxitLauncherService>();
        services.AddSingleton<IUpdateService, UpdateService>();
        services.AddSingleton<IClipboardService, ClipboardService>();
        services.AddTikTokModule(TikTokDatabasePath.Default);
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
        return services;
    }

    /// <summary>
    /// Registers the TikTok order-print module. The database at databasePath is created and migrated lazily by
    /// TikTokWorkspace.EnsureInitializedAsync, off the UI thread.
    /// </summary>
    public static IServiceCollection AddTikTokModule(this IServiceCollection services, string databasePath)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ITikTokRepository>(_ => new SqliteTikTokRepository(databasePath));
        services.AddSingleton<Func<string, IOrderSource>>(_ => filePath => new SellerCenterCsvOrderSource(filePath));
        services.AddSingleton<ISkuConfigSuggester, SkuConfigSuggester>();
        services.AddSingleton<IPdfLibraryScanner, PdfLibraryScanner>();
        services.AddSingleton<IOrderClassifier, OrderClassifier>();
        services.AddSingleton<IPrintPlanBuilder, PrintPlanBuilder>();
        services.AddSingleton<IOrderListExportService, OrderListExportService>();
        services.AddSingleton<ISeparatorPageRenderer, SeparatorPageRenderer>();
        services.AddSingleton<IPrintBatchExporter, PrintBatchExporter>();
        services.AddSingleton<IOrderImportService, OrderImportService>();
        services.AddTransient<TikTokServices>();
        services.AddTransient<TikTokOrdersViewModel>();
        services.AddTransient<TikTokOrdersWindow>();
        services.AddSingleton<Func<TikTokOrdersWindow>>(
            provider => () => provider.GetRequiredService<TikTokOrdersWindow>());
        services.AddSingleton<ITikTokWindowService, TikTokWindowService>();
        return services;
    }
}
