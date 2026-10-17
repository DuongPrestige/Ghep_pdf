using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using PDFPageComposer.App.ViewModels.TikTok;

namespace PDFPageComposer.App.Views;

public partial class TikTokOrdersWindow : Window
{
    private readonly TikTokOrdersViewModel viewModel;

    public TikTokOrdersWindow(TikTokOrdersViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        this.viewModel = viewModel;
        DataContext = viewModel;
        viewModel.Orders.ShowOrderFilesDialog = dialog => new OrderFilesWindow(dialog) { Owner = this }.ShowDialog();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!viewModel.TryConfirmClose())
        {
            e.Cancel = true;
        }

        base.OnClosing(e);
    }

    /// <summary>Double-clicking an order row copies its order id (not when the click hit a button or checkbox).</summary>
    private void OnOrderRowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGridRow { Item: OrderRowViewModel row } || IsInsideButton(e.OriginalSource as DependencyObject))
        {
            return;
        }

        viewModel.Orders.CopyOrderIdCommand.Execute(row);
        e.Handled = true;
    }

    private static bool IsInsideButton(DependencyObject? element)
    {
        for (var current = element; current is not null and not DataGridRow; current = VisualTreeHelper.GetParent(current))
        {
            if (current is ButtonBase)
            {
                return true;
            }
        }

        return false;
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = GetCsvFiles(e).Count > 0 ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        var files = GetCsvFiles(e);
        if (files.Count > 0)
        {
            viewModel.QueueDroppedFiles(files);
        }

        e.Handled = true;
    }

    private static IReadOnlyList<string> GetCsvFiles(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetData(DataFormats.FileDrop) is not string[] files)
        {
            return [];
        }

        return files.Where(ImportPanelViewModel.IsCsv).ToList();
    }
}
