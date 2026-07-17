using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using PDFPageComposer.App.Interfaces;
using PDFPageComposer.App.Services;
using PDFPageComposer.App.ViewModels;
using Serilog;

namespace PDFPageComposer.App;

public partial class App : Application
{
    private ServiceProvider? serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File("logs/pdf-page-composer-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
            .CreateLogger();
        Log.Information("Application startup begin");

        var services = new ServiceCollection();
        ConfigureServices(services);
        Log.Information("Services configured");
        serviceProvider = services.BuildServiceProvider();
        Log.Information("Service provider built");

        var viewModel = serviceProvider.GetRequiredService<MainViewModel>();
        Log.Information("MainViewModel resolved");
        viewModel.CheckRecoveryOnStartupAsync(CancellationToken.None).GetAwaiter().GetResult();
        Log.Information("Recovery check completed");

        Dispatcher.BeginInvoke(() =>
        {
            try
            {
                Log.Information("MainWindow show begin");
                var mainWindow = serviceProvider.GetRequiredService<MainWindow>();
                Log.Information("MainWindow resolved");
                MainWindow = mainWindow;
                mainWindow.Show();
                mainWindow.Activate();
                Log.Information("MainWindow show called");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Failed to show MainWindow");
                MessageBox.Show(ex.Message, "PDF Page Composer startup error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
            }
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        serviceProvider?.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
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
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
    }
}
