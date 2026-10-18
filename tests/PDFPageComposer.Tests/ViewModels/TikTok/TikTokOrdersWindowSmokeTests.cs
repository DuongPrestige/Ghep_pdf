using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Threading;
using PDFPageComposer.App.Views;

namespace PDFPageComposer.Tests.ViewModels.TikTok;

/// <summary>Shows the real window off-screen, visits every tab and fails on any WPF binding error.</summary>
public sealed class TikTokOrdersWindowSmokeTests : IDisposable
{
    private readonly TikTokUiHarness harness = new();

    public void Dispose() => harness.Dispose();

    [Fact]
    public async Task Window_renders_every_tab_without_binding_errors()
    {
        await harness.SeedExampleAsync();
        var bindingErrors = new List<string>();
        Exception? failure = null;

        var thread = new Thread(() =>
        {
            var listener = new CollectingTraceListener(bindingErrors);
            PresentationTraceSources.Refresh();
            PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;
            try
            {
                // Self-check: a broken binding must be reported, otherwise the assertion below proves nothing.
                var probe = new System.Windows.Controls.TextBlock { DataContext = new object() };
                probe.SetBinding(System.Windows.Controls.TextBlock.TextProperty, "MissingProperty");
                PumpUntil(() => true);
                lock (bindingErrors)
                {
                    Assert.Contains(bindingErrors, message => message.Contains("MissingProperty", StringComparison.Ordinal));
                    bindingErrors.Clear();
                }

                var viewModel = harness.CreateViewModel();
                var window = new TikTokOrdersWindow(viewModel)
                {
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = -20000,
                    Top = -20000,
                    ShowActivated = false,
                    ShowInTaskbar = false,
                };
                window.Show();
                PumpUntil(() => viewModel.Orders.AllRows.Count > 0 && !viewModel.Workspace.IsBusy);
                Assert.Null(viewModel.Workspace.ErrorText);

                for (var tab = 0; tab < 5; tab++)
                {
                    viewModel.SelectedTabIndex = tab;
                    PumpUntil(() => true);
                }

                viewModel.History.SelectedPrintBatch = null;
                viewModel.Import.OpenCommand.Execute(null);
                viewModel.Workspace.ShowError("Lỗi thử");
                PumpUntil(() => true);

                // Print preview (O2 prints two sets, so the per-set blocks render) and the file dialog on set 2.
                viewModel.SelectedTabIndex = 0;
                viewModel.Workspace.ErrorText = null;
                PumpUntil(() => true);
                viewModel.Orders.ShowPrintPreviewDialog = preview =>
                {
                    Assert.Contains(preview.Orders, order => order.HasManySets);
                    var previewWindow = OffScreen(new PrintPreviewWindow(preview));
                    previewWindow.Show();
                    PumpUntil(() => true);

                    viewModel.Orders.ShowOrderFilesDialog = dialog =>
                    {
                        var filesWindow = OffScreen(new OrderFilesWindow(dialog));
                        filesWindow.Show();
                        PumpUntil(() => true);
                        filesWindow.Close();
                    };
                    preview.EditSetCommand.Execute(preview.Orders.First(order => order.HasManySets).Sets[1]);
                    PumpUntil(() => true);
                    previewWindow.Close();
                };
                viewModel.Orders.PreviewPrintFileCommand.Execute(null);
                PumpUntil(() => true);
                window.Close();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "The UI thread did not finish in time.");

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }

        Assert.Empty(bindingErrors);
    }

    private static T OffScreen<T>(T window)
        where T : Window
    {
        window.WindowState = WindowState.Normal;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = -20000;
        window.Top = -20000;
        window.ShowActivated = false;
        window.ShowInTaskbar = false;
        return window;
    }

    private static void PumpUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        do
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () => frame.Continue = false);
            Dispatcher.PushFrame(frame);
            if (condition())
            {
                return;
            }

            Thread.Sleep(10);
        }
        while (DateTime.UtcNow < deadline);

        throw new TimeoutException("The window did not finish loading.");
    }

    private sealed class CollectingTraceListener(List<string> messages) : TraceListener
    {
        public override void Write(string? message)
        {
        }

        public override void WriteLine(string? message)
        {
            if (message is not null)
            {
                lock (messages)
                {
                    messages.Add(message);
                }
            }
        }
    }
}
