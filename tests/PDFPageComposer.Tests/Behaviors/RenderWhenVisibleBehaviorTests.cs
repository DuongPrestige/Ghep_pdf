using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using PDFPageComposer.App.Behaviors;

namespace PDFPageComposer.Tests.Behaviors;

public sealed class RenderWhenVisibleBehaviorTests
{
    [Fact]
    public void IsNearViewport_accepts_elements_within_one_viewport_of_the_view()
    {
        var viewport = new Size(200, 300);

        Assert.True(RenderWhenVisibleBehavior.IsNearViewport(new Rect(0, 0, 100, 100), viewport));
        Assert.True(RenderWhenVisibleBehavior.IsNearViewport(new Rect(0, 550, 100, 100), viewport));
        Assert.False(RenderWhenVisibleBehavior.IsNearViewport(new Rect(0, 650, 100, 100), viewport));
        Assert.False(RenderWhenVisibleBehavior.IsNearViewport(new Rect(0, -450, 100, 100), viewport));
        Assert.False(RenderWhenVisibleBehavior.IsNearViewport(Rect.Empty, viewport));
    }

    [Fact]
    public void Only_cards_near_the_view_run_the_command_and_scrolling_runs_the_others_once()
    {
        RunOnSta(() =>
        {
            var executed = new List<int>();
            var command = new RecordingCommand(parameter => executed.Add((int)parameter!));
            var panel = new StackPanel();
            for (var index = 0; index < 50; index++)
            {
                var card = new Border { Height = 100 };
                RenderWhenVisibleBehavior.SetCommand(card, command);
                RenderWhenVisibleBehavior.SetCommandParameter(card, index);
                panel.Children.Add(card);
            }

            var scrollViewer = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var window = new Window
            {
                Content = scrollViewer,
                Width = 200,
                Height = 340,
                Left = -20000,
                Top = -20000,
                ShowActivated = false,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
            };
            window.Show();
            try
            {
                Pump();

                // About 3 cards visible plus one viewport below: far fewer than 50.
                Assert.InRange(executed.Count, 3, 8);
                Assert.Equal(Enumerable.Range(0, executed.Count), executed);

                scrollViewer.ScrollToEnd();
                Pump();
                Assert.Contains(49, executed);
                Assert.DoesNotContain(25, executed);

                scrollViewer.ScrollToHome();
                Pump();
                Assert.Equal(executed.Count, executed.Distinct().Count());
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static void Pump()
    {
        for (var round = 0; round < 5; round++)
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () => frame.Continue = false);
            Dispatcher.PushFrame(frame);
        }
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
            catch (Exception exception)
            {
                failure = ExceptionDispatchInfo.Capture(exception);
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        failure?.Throw();
    }

    private sealed class RecordingCommand(Action<object?> execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => execute(parameter);
    }
}
