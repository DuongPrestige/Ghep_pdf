using System.IO;
using System.Runtime.ExceptionServices;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using PDFPageComposer.App;
using PDFPageComposer.App.Interfaces;
using PDFPageComposer.App.TikTok;
using PDFPageComposer.App.TikTok.Data;
using PDFPageComposer.App.TikTok.Models;
using PDFPageComposer.App.ViewModels.TikTok;
using PDFPageComposer.App.Views;

namespace PDFPageComposer.Tests.TikTok;

/// <summary>Proves the real composition root wires every TikTok service, view model and the window.</summary>
public sealed class CompositionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "gpdf-tiktok-di-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Composition_root_validates_and_resolves_every_TikTok_service()
    {
        using var provider = BuildProvider();

        var repository = Assert.IsType<SqliteTikTokRepository>(provider.GetRequiredService<ITikTokRepository>());
        Assert.Equal(Path.GetFullPath(DatabasePath), repository.DatabasePath);
        Assert.NotNull(provider.GetRequiredService<IOrderImportService>());
        Assert.NotNull(provider.GetRequiredService<ISkuConfigSuggester>());
        Assert.NotNull(provider.GetRequiredService<IPdfLibraryScanner>());
        Assert.NotNull(provider.GetRequiredService<IOrderClassifier>());
        Assert.NotNull(provider.GetRequiredService<IPrintPlanBuilder>());
        Assert.NotNull(provider.GetRequiredService<ISeparatorPageRenderer>());
        Assert.NotNull(provider.GetRequiredService<IPrintBatchExporter>());
        Assert.NotNull(provider.GetRequiredService<ITikTokWindowService>());
        Assert.Same(TimeProvider.System, provider.GetRequiredService<TimeProvider>());

        var source = provider.GetRequiredService<Func<string, IOrderSource>>()("orders.csv");
        Assert.Equal("orders.csv", Path.GetFileName(source.Name));

        var services = provider.GetRequiredService<TikTokServices>();
        Assert.Same(repository, services.Repository);
        Assert.NotNull(services.PageRenderer);
        var viewModel = provider.GetRequiredService<TikTokOrdersViewModel>();
        Assert.NotNull(viewModel.Orders);
        Assert.NotNull(viewModel.Skus);
        Assert.NotNull(viewModel.Library);
        Assert.NotNull(viewModel.History);
        Assert.NotNull(viewModel.Settings);
        Assert.NotNull(viewModel.Import);
        Assert.NotSame(viewModel, provider.GetRequiredService<TikTokOrdersViewModel>());
    }

    [Fact]
    public void Orders_window_xaml_loads_from_the_composition_root()
    {
        RunOnSta(() =>
        {
            using var provider = BuildProvider();
            var window = provider.GetRequiredService<Func<TikTokOrdersWindow>>()();
            try
            {
                Assert.IsType<TikTokOrdersViewModel>(window.DataContext);
                Assert.False(string.IsNullOrWhiteSpace(window.Title));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Order_files_window_xaml_loads()
    {
        RunOnSta(() =>
        {
            var order = new TikTokOrder(
                "O1", 1, "Chờ vận chuyển", DateTimeOffset.Now, null, null, null, true, 1, 1, null, null, null, false, null,
                [new TikTokOrderLine("O1", "SKU", 1, "Card", "20 CARD, 1 Mặt", 1)]);
            var dialog = new OrderFilesViewModel(order, new Dictionary<string, SkuConfig>(), [], [], [], renderer: null);
            var window = new OrderFilesWindow(dialog);
            try
            {
                Assert.Same(dialog, window.DataContext);
                Assert.NotNull(window.GetBindingExpression(System.Windows.Window.TitleProperty));
                Assert.Contains("O1", dialog.Title, StringComparison.Ordinal);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task Composed_repository_initializes_the_database()
    {
        using var provider = BuildProvider();
        var repository = provider.GetRequiredService<ITikTokRepository>();

        await repository.InitializeAsync(CancellationToken.None);
        var shop = await repository.AddShopAsync("Shop thử", CancellationToken.None);

        Assert.Equal([shop], await repository.GetShopsAsync(CancellationToken.None));
        Assert.True(File.Exists(DatabasePath));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string DatabasePath => Path.Combine(_root, "tiktok.db");

    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddPdfPageComposer();
        // Point the module at a temporary database; the last registration wins.
        services.AddSingleton<ITikTokRepository>(_ => new SqliteTikTokRepository(DatabasePath));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    private static void RunOnSta(Action action)
    {
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ExceptionDispatchInfo.Capture(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        failure?.Throw();
    }
}
