using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PDFPageComposer.App.ViewModels.TikTok;

namespace PDFPageComposer.App.Views;

/// <summary>Modal "Xem / chọn file in" dialog of one order.</summary>
public partial class OrderFilesWindow : Window
{
    private readonly OrderFilesViewModel viewModel;

    public OrderFilesWindow(OrderFilesViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        this.viewModel = viewModel;
        DataContext = viewModel;
        viewModel.CloseRequested += (_, _) => Close();
    }

    private void OnLibraryPageClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: PageThumbViewModel thumb })
        {
            viewModel.TogglePageCommand.Execute(thumb);
            e.Handled = true;
        }
    }

    /// <summary>The wheel scrolls the file list; Shift+wheel scrolls a file's page strip sideways (like the composer).</summary>
    private void OnPageStripMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer strip)
        {
            return;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            strip.ScrollToHorizontalOffset(strip.HorizontalOffset - e.Delta);
        }
        else if (FindDescendant<ScrollViewer>(LibraryList) is { } list)
        {
            list.ScrollToVerticalOffset(list.VerticalOffset - e.Delta);
        }

        e.Handled = true;
    }

    private static T? FindDescendant<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match)
            {
                return match;
            }

            if (FindDescendant<T>(child) is { } descendant)
            {
                return descendant;
            }
        }

        return null;
    }
}
