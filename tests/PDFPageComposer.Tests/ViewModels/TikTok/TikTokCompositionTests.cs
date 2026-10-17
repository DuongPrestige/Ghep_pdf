using Microsoft.Extensions.DependencyInjection;
using PDFPageComposer.App;
using PDFPageComposer.App.Interfaces;
using PDFPageComposer.App.TikTok;
using PDFPageComposer.App.ViewModels.TikTok;

namespace PDFPageComposer.Tests.ViewModels.TikTok;

public sealed class TikTokCompositionTests
{
    [Fact]
    public void App_registrations_resolve_the_tiktok_window_dependencies()
    {
        var services = new ServiceCollection();
        services.AddPdfPageComposer();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });

        var first = provider.GetRequiredService<TikTokOrdersViewModel>();
        var second = provider.GetRequiredService<TikTokOrdersViewModel>();
        Assert.NotSame(first, second);
        Assert.Same(provider.GetRequiredService<ITikTokRepository>(), first.Workspace.Services.Repository);
        Assert.NotNull(provider.GetRequiredService<ITikTokWindowService>());
        Assert.NotNull(provider.GetRequiredService<IClipboardService>());
        Assert.NotNull(provider.GetRequiredService<IPrintBatchExporter>());
        Assert.NotNull(provider.GetRequiredService<IOrderImportService>());
    }
}
