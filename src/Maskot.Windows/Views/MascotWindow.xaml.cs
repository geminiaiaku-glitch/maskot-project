using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Maskot.Windows.Platform;

namespace Maskot.Windows.Views;

public partial class MascotWindow : Window
{
    private bool? _lastNotch;
    internal App Host => (App)Application.Current;
    public MascotWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            Desktop.MakePassive(this);
            HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(NativeMessage);
        };
        Closed += (_, _) => Desktop.ReleaseEscape(this);
        Closing += (_, e) => { if (!Host.Exiting) { e.Cancel = true; Hide(); } };
    }
    private IntPtr NativeMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == 0x0312 && wParam.ToInt32() == Desktop.DragEscapeId && Host.Controller.PointerHeld)
        { CancelDrag(); handled = true; }
        return IntPtr.Zero;
    }
    private void PointerDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        Host.Controller.Press(Desktop.Cursor); Desktop.CaptureEscape(this); Mouse.Capture(this); e.Handled = true;
    }
    private void PointerMove(object sender, MouseEventArgs e)
    {
        if (!Host.Controller.PointerHeld) return;
        Host.Controller.MovePointer(Desktop.Cursor, Host.Displays);
        if (Host.Controller.Dragging) Host.ClosePanel();
        Host.Render();
    }
    private void PointerUp(object sender, MouseButtonEventArgs e)
    {
        var click = Host.Controller.Release(Host.Displays);
        Desktop.ReleaseEscape(this);
        Mouse.Capture(null);
        if (click) Host.TogglePanel();
        Host.SaveSoon(); Host.Render(); e.Handled = true;
    }
    private void CaptureLost(object sender, MouseEventArgs e)
    {
        Desktop.ReleaseEscape(this);
        if (Host.Controller.PointerHeld) { Host.Controller.CancelDrag(); Host.Render(); }
    }
    internal void CancelDrag()
    {
        Desktop.ReleaseEscape(this);
        Host.Controller.CancelDrag(); Mouse.Capture(null); Host.Render();
    }
    private void FilesDragged(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? ((e.AllowedEffects & DragDropEffects.Link) != 0 ? DragDropEffects.Link : DragDropEffects.Copy)
            : DragDropEffects.None;
        e.Handled = true;
    }
    private void FilesDropped(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths) Host.AddFiles(paths);
        e.Handled = true;
    }
    private void QuickMenu(object sender, MouseButtonEventArgs e)
    {
        Host.Controller.SetPanel(true);
        var menu = new ContextMenu();
        void Item(string text, Action action) { var i = new MenuItem { Header = text }; i.Click += (_, _) => action(); menu.Items.Add(i); }
        Item("Jalan-jalan", Host.Walk);
        Item("Diam di sini", Host.Stay);
        Item("Pengaturan", () => Host.OpenPanel(true));
        Item("Lirik & karaoke", Host.OpenLyricsPanel);
        menu.Items.Add(new Separator()); Item("Keluar", Host.Quit);
        menu.Closed += (_, _) => { if (!Host.PanelVisible) Host.Controller.SetPanel(false); };
        ContextMenu = menu; menu.IsOpen = true; e.Handled = true;
    }
    internal void UpdateAppearance()
    {
        var c = Host.Controller;
        var cue = Host.Cue;
        var notch = c.Mode == MascotMode.Notch;
        Notch.Visibility = notch ? Visibility.Visible : Visibility.Collapsed;
        Character.Visibility = notch ? Visibility.Collapsed : Visibility.Visible;
        if (_lastNotch is not null && _lastNotch != notch && Host.State.Preferences.ExtraAnimations)
        {
            FrameworkElement visual = notch ? Notch : Character;
            visual.RenderTransformOrigin = new(.5, notch ? 0 : .5);
            var scale = new ScaleTransform(1, .8); visual.RenderTransform = scale;
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(.8, 1, TimeSpan.FromMilliseconds(180)) { EasingFunction = ease });
            visual.BeginAnimation(OpacityProperty, new DoubleAnimation(.3, 1, TimeSpan.FromMilliseconds(180)));
        }
        _lastNotch = notch;
        Character.SetPose(c.Pose, c.FacingRight, Host.State.Preferences.ExtraAnimations);
        Character.SetPerformance(cue.Mood);
        var timer = Host.State.Timer;
        var remaining = TimeSpan.FromSeconds(timer.Remaining(DateTimeOffset.UtcNow));
        var text = $"{(int)remaining.TotalMinutes:00}:{remaining.Seconds:00}";
        SmallTimer.Text = text; NotchTimer.Text = timer.Running ? text : DateTime.Now.ToString("HH:mm");
        NotchBars.IsActive = Host.ActiveMedia?.Playing == true && Host.State.Preferences.ExtraAnimations;
        TimerPill.Visibility = timer.Running && !notch ? Visibility.Visible : Visibility.Collapsed;
        NotchLabel.Text = Host.ActiveMedia is { } m ? (cue.Paused ? "Ⅱ " : "") + m.Title : timer.Running ? "Waktunya fokus" : "Momo";
        NotchLyrics.Visibility = notch && cue.ExpandedNotch ? Visibility.Visible : Visibility.Collapsed;
        NotchLyricLine.Text = cue.Current; NotchNextLine.Text = cue.Next;
        ToolTip = c.Mode == MascotMode.Stationary ? "Diam di sini · klik kanan → Jalan-jalan untuk berjalan lagi" : "Klik untuk membuka panel · seret ke tengah atas untuk notch";
    }
}
