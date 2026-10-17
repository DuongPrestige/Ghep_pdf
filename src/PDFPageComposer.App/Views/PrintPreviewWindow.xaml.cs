using System.Windows;
using PDFPageComposer.App.ViewModels.TikTok;

namespace PDFPageComposer.App.Views;

/// <summary>Modal "Xem trước file in" of the ticked TikTok orders.</summary>
public partial class PrintPreviewWindow : Window
{
    public PrintPreviewWindow(PrintPreviewViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += (_, _) => Close();
    }
}
