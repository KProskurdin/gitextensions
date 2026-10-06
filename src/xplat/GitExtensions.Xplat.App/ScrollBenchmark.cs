using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Developer aid for the revision graph's frame-rate target (PLAN.md M3.5): scrolls the commit list a fixed distance per
///  frame with the platform renderer and reports how long the frames took. It needs a real desktop session (or xvfb); the
///  headless test platform does not render.
/// </summary>
internal static class ScrollBenchmark
{
    private const int Frames = 600;
    private const double PixelsPerFrame = 40;

    // A frame slower than one and a half 60 Hz frames is counted as a visible stutter.
    private const double SlowFrameMilliseconds = 1000.0 / 60 * 1.5;

    public static async Task<string> RunAsync(TopLevel topLevel, ListBox list, int loadedCommits, TimeSpan loadTime)
    {
        ScrollViewer scroller = list.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault()
                                ?? throw new InvalidOperationException("The list has no scroll viewer.");
        List<double> frameTimes = new(Frames);
        TaskCompletionSource done = new();
        Stopwatch clock = Stopwatch.StartNew();
        TimeSpan? previous = null;

        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        topLevel.RequestAnimationFrame(OnFrame);
        await done.Task;

        frameTimes.Sort();
        double average = frameTimes.Average();
        double p95 = frameTimes[(int)(frameTimes.Count * 0.95)];
        double slow = frameTimes.Count(time => time > SlowFrameMilliseconds) * 100.0 / frameTimes.Count;
        return string.Create(CultureInfo.InvariantCulture,
            $"commits={loadedCommits} load_ms={loadTime.TotalMilliseconds:0} frames={frameTimes.Count} avg_frame_ms={average:0.00} " +
            $"p95_frame_ms={p95:0.00} fps={1000 / average:0.0} slow_frames_pct={slow:0.0} os={RuntimeInformation.OSDescription}");

        // Each frame moves the list on and asks for the next frame, so the measured interval includes layout and render.
        void OnFrame(TimeSpan timestamp)
        {
            TimeSpan now = clock.Elapsed;
            if (previous is { } last)
            {
                frameTimes.Add((now - last).TotalMilliseconds);
            }

            previous = now;
            double y = scroller.Offset.Y + PixelsPerFrame;
            scroller.Offset = scroller.Offset.WithY(y >= scroller.Extent.Height - scroller.Viewport.Height ? 0 : y);
            if (frameTimes.Count >= Frames)
            {
                done.SetResult();
                return;
            }

            topLevel.RequestAnimationFrame(OnFrame);
        }
    }
}
