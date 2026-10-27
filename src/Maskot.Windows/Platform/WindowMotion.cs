using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace Maskot.Windows.Platform;

internal sealed class WindowMotion(Window window) : IDisposable
{
    private readonly DispatcherTimer _timer = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly Stopwatch _clock = new();
    private RectD? _target;
    private RectD _from;
    private bool _subscribed;

    internal void Move(RectD target, bool animate)
    {
        if (_target == target)
        {
            if (!animate) { _timer.Stop(); Desktop.Position(window, new(target.X, target.Y), target.Width, target.Height); }
            return;
        }
        _target = target;
        _from = Desktop.WindowBounds(window);
        if (!animate || !window.IsVisible || _from.Width <= 0)
        { _timer.Stop(); Desktop.Position(window, new(target.X, target.Y), target.Width, target.Height); return; }
        if (!_subscribed) { _timer.Tick += Frame; _subscribed = true; }
        _clock.Restart(); _timer.Start(); Frame(null, EventArgs.Empty);
    }
    private void Frame(object? sender, EventArgs e)
    {
        if (_target is not { } target || !window.IsVisible) { _timer.Stop(); return; }
        var t = Math.Clamp(_clock.Elapsed.TotalMilliseconds / 180, 0, 1);
        var ease = 1 - Math.Pow(1 - t, 3);
        double Lerp(double a, double b) => a + (b - a) * ease;
        Desktop.Position(window, new(Lerp(_from.X, target.X), Lerp(_from.Y, target.Y)), Lerp(_from.Width, target.Width), Lerp(_from.Height, target.Height));
        if (t >= 1) _timer.Stop();
    }
    internal void Reset() { _timer.Stop(); _target = null; }
    public void Dispose() { _timer.Stop(); if (_subscribed) _timer.Tick -= Frame; }
}
