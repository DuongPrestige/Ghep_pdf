using System.Windows;
using PDFPageComposer.App.Interfaces;
using PDFPageComposer.App.Views;

namespace PDFPageComposer.App.Services;

public sealed class TikTokWindowService : ITikTokWindowService
{
    private readonly Func<TikTokOrdersWindow> windowFactory;
    private TikTokOrdersWindow? openWindow;

    public TikTokWindowService(Func<TikTokOrdersWindow> windowFactory)
    {
        ArgumentNullException.ThrowIfNull(windowFactory);
        this.windowFactory = windowFactory;
    }

    public void ShowOrdersWindow()
    {
        if (openWindow is not null)
        {
            if (openWindow.WindowState == WindowState.Minimized)
            {
                openWindow.WindowState = WindowState.Normal;
            }

            openWindow.Activate();
            return;
        }

        var window = windowFactory();
        var owner = Application.Current?.MainWindow;
        if (owner is not null && !ReferenceEquals(owner, window) && owner.IsLoaded)
        {
            window.Owner = owner;
        }

        window.Closed += OnWindowClosed;
        openWindow = window;
        window.WindowState = WindowState.Maximized;
        window.Show();
        window.Activate();
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (sender is TikTokOrdersWindow window)
        {
            window.Closed -= OnWindowClosed;
        }

        openWindow = null;
    }
}
