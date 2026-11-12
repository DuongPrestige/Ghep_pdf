using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Threading;
using PDFPageComposer.App;
using PDFPageComposer.App.Interfaces;

namespace PDFPageComposer.Tests.ViewModels;

/// <summary>Shows the real main window off-screen in both modes and fails on any WPF binding error.</summary>
public sealed partial class MainViewModelTests
{
    [Fact]
    public void Main_window_renders_the_duplex_tray_without_binding_errors()
    {
        var bindingErrors = new List<string>();
        Exception? failure = null;

        var thread = new Thread(() =>
        {
            var listener = new BindingErrorListener(bindingErrors);
            PresentationTraceSources.Refresh();
            PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;
            try
            {
                var photos = CreateSourceFile(@"D:\Card\PMC.pdf", 3, "pmc");
                var signatures = CreateSourceFile(@"D:\Card\chu ky.pdf", 2, "sig");
                var viewModel = CreateViewModelWith(photos, signatures);
                var window = new MainWindow(viewModel, new NoTikTokWindowService())
                {
                    WindowState = WindowState.Normal,
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = -20000,
                    Top = -20000,
                    ShowActivated = false,
                    ShowInTaskbar = false,
                };
                window.Show();
                PumpFrames();

                SelectPagesAndAddToOutput(viewModel, photos, 1);
                viewModel.IsDuplexMode = true;
                SelectPagesAndAddToOutput(viewModel, photos, 1, 2, 3);
                SelectPagesAndAddToOutput(viewModel, signatures, 1);
                SelectPagesAndAddToOutput(viewModel, photos, 1, 2);
                SelectPagesAndAddToOutput(viewModel, signatures, 1, 2, 2);
                SelectPagesAndAddToOutput(viewModel, photos, 3);
                PumpFrames();
                Assert.True(viewModel.HasDuplexProblem);

                viewModel.IsDuplexMode = false;
                PumpFrames();
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

        Assert.True(bindingErrors.Count == 0, string.Join(Environment.NewLine, bindingErrors.Distinct()));
    }

    private static void PumpFrames()
    {
        for (var pass = 0; pass < 5; pass++)
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () => frame.Continue = false);
            Dispatcher.PushFrame(frame);
            Thread.Sleep(10);
        }
    }

    private sealed class NoTikTokWindowService : ITikTokWindowService
    {
        public void ShowOrdersWindow()
        {
        }
    }

    private sealed class BindingErrorListener(List<string> messages) : TraceListener
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
