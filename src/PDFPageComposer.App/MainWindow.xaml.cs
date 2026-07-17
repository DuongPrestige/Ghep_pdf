using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using PDFPageComposer.App.Models;
using PDFPageComposer.App.ViewModels;

namespace PDFPageComposer.App;

public partial class MainWindow : Window
{
    private const string OutputTrayItemDragFormat = "PDFPageComposer.OutputTrayItem";
    private const string OutputGroupDragFormat = "PDFPageComposer.OutputGroup";
    private readonly MainViewModel viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        DataContext = viewModel;
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = HasPdfFiles(e)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (!HasPdfFiles(e))
        {
            e.Handled = true;
            return;
        }

        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        await viewModel.ImportPdfFilesAsync(files, CancellationToken.None);
        e.Handled = true;
    }

    private static bool HasPdfFiles(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return false;
        }

        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        return files.Any(file => string.Equals(Path.GetExtension(file), ".pdf", StringComparison.OrdinalIgnoreCase));
    }

    private void OnThumbnailClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount > 1)
        {
            e.Handled = true;
            return;
        }

        if (sender is not FrameworkElement { DataContext: SourcePdfPage page })
        {
            return;
        }

        var modifiers = Keyboard.Modifiers;
        var gesture = modifiers.HasFlag(ModifierKeys.Shift)
            ? SelectionGesture.Range
            : modifiers.HasFlag(ModifierKeys.Control)
                ? SelectionGesture.AddOrRemove
                : SelectionGesture.Toggle;

        var request = new PageSelectionRequest(page, gesture);
        if (viewModel.SelectPageCommand.CanExecute(request))
        {
            viewModel.SelectPageCommand.Execute(request);
        }

        e.Handled = true;
    }

    private async void OnThumbnailMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2 || sender is not FrameworkElement { DataContext: SourcePdfPage page })
        {
            return;
        }

        if (viewModel.OpenPreviewCommand.CanExecute(page))
        {
            await viewModel.OpenPreviewCommand.ExecuteAsync(page);
        }

        e.Handled = true;
    }

    private async void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            return;
        }

        if (viewModel.IsPreviewOpen)
        {
            await viewModel.AdjustPreviewZoomCommand.ExecuteAsync(e.Delta > 0 ? 0.1 : -0.1);
        }
        else
        {
            viewModel.AdjustThumbnailZoom(e.Delta > 0 ? 0.1 : -0.1);
        }

        e.Handled = true;
    }

    private void OnPageStripMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) || sender is not ScrollViewer scrollViewer)
        {
            return;
        }

        scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - e.Delta);
        e.Handled = true;
    }

    private void OnOutputPreviewGridMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer)
        {
            return;
        }

        var direction = e.Delta < 0 ? 1 : -1;
        viewModel.MoveOutputPreviewGridPage(direction);
        e.Handled = true;
    }

    private void OnSourceFileListMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) ||
            Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ||
            FindDescendant<ScrollViewer>(SourceFileList) is not { } scrollViewer)
        {
            return;
        }

        if (e.Delta < 0)
        {
            scrollViewer.LineDown();
        }
        else
        {
            scrollViewer.LineUp();
        }

        e.Handled = true;
    }

    private async void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && viewModel.IsPreviewOpen)
        {
            viewModel.ClosePreviewCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Enter ||
            Keyboard.FocusedElement is not FrameworkElement { DataContext: SourcePdfPage page })
        {
            return;
        }

        if (viewModel.OpenPreviewCommand.CanExecute(page))
        {
            await viewModel.OpenPreviewCommand.ExecuteAsync(page);
        }

        e.Handled = true;
    }

    private void OnOutputItemPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed ||
            IsFromButton(e.OriginalSource as DependencyObject) ||
            sender is not FrameworkElement { DataContext: OutputTrayItemView itemView })
        {
            return;
        }

        var data = new DataObject(OutputTrayItemDragFormat, itemView);
        DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Move);
    }

    private void OnOutputItemDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(OutputTrayItemDragFormat)
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnOutputItemDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(OutputTrayItemDragFormat) ||
            e.Data.GetData(OutputTrayItemDragFormat) is not OutputTrayItemView source ||
            sender is not FrameworkElement { DataContext: OutputTrayItemView target })
        {
            e.Handled = true;
            return;
        }

        var request = new OutputItemMoveRequest(source.Item, target.Item);
        if (viewModel.MoveOutputItemBeforeCommand.CanExecute(request))
        {
            viewModel.MoveOutputItemBeforeCommand.Execute(request);
        }

        e.Handled = true;
    }

    private void OnOutputGroupPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed ||
            IsFromButton(e.OriginalSource as DependencyObject) ||
            sender is not FrameworkElement { DataContext: OutputTrayItemView itemView })
        {
            return;
        }

        var data = new DataObject(OutputGroupDragFormat, itemView.Group);
        DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Move);
    }

    private void OnOutputGroupDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(OutputGroupDragFormat)
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnOutputGroupDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(OutputGroupDragFormat) ||
            e.Data.GetData(OutputGroupDragFormat) is not OutputGroup source ||
            sender is not FrameworkElement { DataContext: OutputTrayItemView target })
        {
            e.Handled = true;
            return;
        }

        var request = new OutputGroupMoveRequest(source, target.Group);
        if (viewModel.MoveOutputGroupBeforeCommand.CanExecute(request))
        {
            viewModel.MoveOutputGroupBeforeCommand.Execute(request);
        }

        e.Handled = true;
    }

    private void OnDuplicateSelectedOutputItems(object sender, RoutedEventArgs e)
    {
        var selectedItems = OutputTrayList.SelectedItems
            .OfType<OutputTrayItemView>()
            .ToArray();
        if (viewModel.DuplicateOutputItemsCommand.CanExecute(selectedItems))
        {
            viewModel.DuplicateOutputItemsCommand.Execute(selectedItems);
        }
    }

    private void OnDeleteSelectedOutputItems(object sender, RoutedEventArgs e)
    {
        var selectedItems = OutputTrayList.SelectedItems
            .OfType<OutputTrayItemView>()
            .ToArray();
        if (viewModel.DeleteOutputItemsCommand.CanExecute(selectedItems))
        {
            viewModel.DeleteOutputItemsCommand.Execute(selectedItems);
        }
    }

    private static bool IsFromButton(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is ButtonBase)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private static T? FindDescendant<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T typedChild)
            {
                return typedChild;
            }

            var descendant = FindDescendant<T>(child);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }
}
