using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace PDFPageComposer.App.Behaviors;

/// <summary>
/// Runs Command once, when the element first comes into (or near) the view of its scroll viewer. Used for page
/// thumbnails in non-virtualized panels (output tray, preview grids): hundreds of cards can exist, but only the pages
/// the user scrolls to are rendered. Without a scroll viewer ancestor the command runs when the element loads.
/// </summary>
public static class RenderWhenVisibleBehavior
{
    /// <summary>Elements this far outside the viewport (in viewport heights/widths) still count as visible.</summary>
    private const double PreloadViewports = 1.0;

    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.RegisterAttached(
            "Command",
            typeof(ICommand),
            typeof(RenderWhenVisibleBehavior),
            new PropertyMetadata(null, OnCommandChanged));

    public static readonly DependencyProperty CommandParameterProperty =
        DependencyProperty.RegisterAttached(
            "CommandParameter",
            typeof(object),
            typeof(RenderWhenVisibleBehavior),
            new PropertyMetadata(null));

    private static readonly DependencyProperty WatchProperty =
        DependencyProperty.RegisterAttached(
            "Watch",
            typeof(Watch),
            typeof(RenderWhenVisibleBehavior),
            new PropertyMetadata(null));

    public static void SetCommand(DependencyObject element, ICommand? value) => element.SetValue(CommandProperty, value);

    public static ICommand? GetCommand(DependencyObject element) => (ICommand?)element.GetValue(CommandProperty);

    public static void SetCommandParameter(DependencyObject element, object? value) => element.SetValue(CommandParameterProperty, value);

    public static object? GetCommandParameter(DependencyObject element) => element.GetValue(CommandParameterProperty);

    /// <summary>True when element intersects viewport (in viewport coordinates) enlarged by the preload margin.</summary>
    public static bool IsNearViewport(Rect elementBounds, Size viewport)
    {
        if (elementBounds.IsEmpty || viewport.Width <= 0 || viewport.Height <= 0)
        {
            return false;
        }

        var area = new Rect(
            -viewport.Width * PreloadViewports,
            -viewport.Height * PreloadViewports,
            viewport.Width * (1 + (2 * PreloadViewports)),
            viewport.Height * (1 + (2 * PreloadViewports)));
        return area.IntersectsWith(elementBounds);
    }

    private static void OnCommandChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not FrameworkElement element)
        {
            return;
        }

        element.Loaded -= OnLoaded;
        element.Unloaded -= OnUnloaded;
        if (e.NewValue is ICommand)
        {
            element.Loaded += OnLoaded;
            element.Unloaded += OnUnloaded;
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        var element = (FrameworkElement)sender;
        StopWatching(element);
        var scrollViewer = FindAncestor<ScrollViewer>(element);
        if (scrollViewer is null)
        {
            Execute(element);
            return;
        }

        var watch = new Watch(element, scrollViewer);
        element.SetValue(WatchProperty, watch);
        scrollViewer.ScrollChanged += watch.OnScrollChanged;

        // Layout is not final yet while Loaded runs; check once it is.
        element.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, watch.Check);
    }

    private static void OnUnloaded(object sender, RoutedEventArgs e) => StopWatching((FrameworkElement)sender);

    private static void StopWatching(FrameworkElement element)
    {
        if (element.GetValue(WatchProperty) is Watch watch)
        {
            watch.Stop();
            element.ClearValue(WatchProperty);
        }
    }

    private static void Execute(FrameworkElement element)
    {
        var command = GetCommand(element);
        var parameter = GetCommandParameter(element);
        if (command?.CanExecute(parameter) == true)
        {
            command.Execute(parameter);
        }
    }

    private static T? FindAncestor<T>(DependencyObject element)
        where T : DependencyObject
    {
        for (var current = VisualTreeHelper.GetParent(element); current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is T match)
            {
                return match;
            }
        }

        return null;
    }

    private sealed class Watch(FrameworkElement element, ScrollViewer scrollViewer)
    {
        private bool done;

        public void OnScrollChanged(object sender, ScrollChangedEventArgs e) => Check();

        public void Check()
        {
            if (done || !element.IsLoaded || !element.IsVisible || !scrollViewer.IsAncestorOf(element))
            {
                return;
            }

            var bounds = element.TransformToAncestor(scrollViewer).TransformBounds(new Rect(element.RenderSize));
            if (!IsNearViewport(bounds, new Size(scrollViewer.ActualWidth, scrollViewer.ActualHeight)))
            {
                return;
            }

            Stop();
            element.ClearValue(WatchProperty);
            Execute(element);
        }

        public void Stop()
        {
            done = true;
            scrollViewer.ScrollChanged -= OnScrollChanged;
        }
    }
}
