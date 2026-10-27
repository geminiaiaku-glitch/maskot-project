using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Maskot.Windows.Views;

public partial class ActivityBars : UserControl
{
    private readonly Stopwatch _clock = new();
    private readonly DispatcherTimer _timer;
    private bool _active;
    public bool IsActive { get => _active; set { if (_active == value) return; _active = value; UpdateClock(); } }
    public ActivityBars()
    {
        InitializeComponent();
        _timer = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(80) };
        _timer.Tick += (_, _) => Draw();
        Loaded += (_, _) => UpdateClock(); IsVisibleChanged += (_, _) => UpdateClock();
        Unloaded += (_, _) => { _timer.Stop(); _clock.Stop(); };
    }
    private void UpdateClock()
    {
        if (_timer is null) return;
        if (IsLoaded && IsVisible && IsActive) { _clock.Start(); _timer.Start(); }
        else { _timer.Stop(); _clock.Stop(); }
        Draw();
    }
    private void Draw()
    {
        var bars = new[] { A, B, C, D }; var t = _clock.Elapsed.TotalSeconds;
        for (var i = 0; i < bars.Length; i++) bars[i].Height = IsActive ? 4 + (Math.Sin(t * (5 + i) + i * 1.7) + 1) * 5.5 : 3;
        Opacity = IsActive ? 1 : .35;
    }
}
