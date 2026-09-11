using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace Gym.TestUtilities.Wpf;

/// <summary>
/// Runs test code on a fresh STA thread that owns a WPF <see cref="Dispatcher"/>, the way the UI
/// thread of a real WPF application does. Exceptions are re-thrown on the calling test thread.
/// </summary>
public static class Sta
{
    public static void Run(Action body, TimeSpan? timeout = null) =>
        RunAsync(() =>
        {
            body();
            return Task.CompletedTask;
        }, timeout);

    /// <summary>
    /// Runs an async body on the STA thread, pumping the dispatcher until the body completes,
    /// so continuations that return to the UI thread work exactly as they would in the app.
    /// </summary>
    public static void RunAsync(Func<Task> body, TimeSpan? timeout = null)
    {
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            try
            {
                var task = body();
                var frame = new DispatcherFrame();
                task.ContinueWith(_ => frame.Continue = false, TaskScheduler.FromCurrentSynchronizationContext());
                Dispatcher.PushFrame(frame);
                task.GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                failure = ExceptionDispatchInfo.Capture(ex);
            }
            finally
            {
                dispatcher.InvokeShutdown();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        if (!thread.Join(timeout ?? TimeSpan.FromSeconds(20)))
        {
            throw new TimeoutException("The STA test body did not finish in time (is the UI thread blocked?).");
        }

        failure?.Throw();
    }

    /// <summary>Processes all pending dispatcher work at or above Background priority (bindings, layout, posts).</summary>
    public static void DoEvents()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, () => frame.Continue = false);
        Dispatcher.PushFrame(frame);
    }

    /// <summary>Pumps the dispatcher until the condition holds (bounded by a safety timeout).</summary>
    public static void PumpUntil(Func<bool> condition, TimeSpan? timeout = null, string because = "condition")
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Timed out waiting for {because}.");
            }

            DoEvents();
            Thread.Sleep(1);
        }
    }

    /// <summary>Lays out an element off-screen so that templates are applied and bindings are live.</summary>
    public static T Realize<T>(T element, double width = 800, double height = 600) where T : UIElement
    {
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
        element.UpdateLayout();
        DoEvents();
        return element;
    }

    public static T? FindDescendant<T>(DependencyObject root, Func<T, bool>? predicate = null) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match && (predicate is null || predicate(match)))
            {
                return match;
            }

            var nested = FindDescendant(child, predicate);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }
}
