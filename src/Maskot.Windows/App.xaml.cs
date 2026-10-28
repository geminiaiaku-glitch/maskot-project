using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Maskot.Windows.Platform;
using Maskot.Windows.Views;
using Maskot.Windows.Diagnostics;
using System.Windows.Interop;
using System.Windows.Media;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace Maskot.Windows;

public partial class App : Application
{
    internal AppState State { get; private set; } = new();
    internal MascotController Controller { get; } = new();
    internal IReadOnlyList<DisplayArea> Displays { get; private set; } = [];
    internal MediaService Media { get; } = new();
    internal KaraokeSession Karaoke { get; private set; } = null!;
    internal NotificationService Notifications { get; private set; } = null!;
    private FixtureNotificationSource? _fixtureNotifications;
    private NotificationWindow? _notificationWindow;
    private DesktopNotice? _demoNotification;
    private DateTimeOffset _demoNotificationUntil;
    private bool _sessionLocked;
    internal NotificationWindow? NotificationForDiagnostics => _notificationWindow;
    private LyricsClient? _lyricsClient;
    private FixtureLyricsSource? _fixtureLyrics;
    private MediaView? _fixtureMedia;
    private TimeSpan _fixturePosition;
    private bool _fixtureActive;
    internal MediaView? ActiveMedia => _fixtureActive ? _fixtureMedia : Media.Current;
    internal TimeSpan ActivePosition => _fixtureActive ? _fixturePosition : Media.Position;
    internal string ActiveMediaStatus => _fixtureActive ? "Sesi uji · tanpa audio nyata." : Media.Status;
    internal KaraokeCue Cue => KaraokeCue.Create(Karaoke.Document, !string.IsNullOrWhiteSpace(ActiveMedia?.Title), ActiveMedia?.Playing == true, ActivePosition, State.Preferences.LyricsEnabled);
    internal LyricBubbleWindow? BubbleForDiagnostics => _lyricBubble;
    internal int FixtureLyricsRequests => _fixtureLyrics?.Requests ?? 0;
    internal bool Exiting { get; private set; }
    internal bool PanelVisible => _panel?.IsVisible == true;
    private bool NotificationSettingsVisible => PanelVisible && _panel?.Tabs.SelectedIndex == 3;
    internal MascotWindow MascotForDiagnostics => _mascot!;
    internal PanelWindow? PanelForDiagnostics => _panel;
    internal bool SmokeMode { get; private set; }
    private string? _smokeOutput;
    private MascotWindow? _mascot;
    private PanelWindow? _panel;
    private WindowMotion? _panelMotion;
    private readonly NotchPeek _peek = new();
    private bool _previewPanel;
    private DockHintWindow? _hint;
    private LyricBubbleWindow? _lyricBubble;
    private Forms.NotifyIcon? _tray;
    private Drawing.Icon? _icon;
    private StateStore _store = null!;
    private Mutex? _instance;
    private readonly Stopwatch _clock = new();
    private DispatcherTimer? _tick;
    private double _lastTick, _lastSecond, _lastSave;
    private DateTimeOffset? _saveAt;
    private DateTimeOffset _forceVisibleUntil;
    private string? _lastSaveError;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        SmokeMode = e.Args.Contains("--self-test");
        if (SmokeMode)
        {
            var outputIndex = Array.IndexOf(e.Args, "--self-test-output");
            _smokeOutput = outputIndex >= 0 && outputIndex + 1 < e.Args.Length ? e.Args[outputIndex + 1] : Path.Combine(Path.GetTempPath(), "maskot-smoke-output");
            Directory.CreateDirectory(_smokeOutput);
        }
        if (SmokeMode) RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
        _instance = new Mutex(true, SmokeMode ? @"Local\Maskot.Desktop.Smoke." + Guid.NewGuid() : @"Local\Maskot.Desktop.Momo", out var created);
        if (!created) { MessageBox.Show("Maskot sudah berjalan. Gunakan ikon Momo di system tray.", "Maskot"); Shutdown(); return; }
        DispatcherUnhandledException += Unhandled;
        _store = new(SmokeMode ? Path.Combine(Path.GetTempPath(), "maskot-smoke-" + Guid.NewGuid())
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Maskot"));
        State = _store.Load(); State.Normalize();
        if (SmokeMode) { _fixtureNotifications = new(); Notifications = new(_fixtureNotifications, pollAutomatically: false); }
        else Notifications = new();
        Notifications.Changed += NotificationsChanged;
        if (SmokeMode) { _fixtureLyrics = new(); Karaoke = new(_fixtureLyrics); }
        else { _lyricsClient = new(); Karaoke = new(_lyricsClient); }
        Karaoke.Changed += KaraokeChanged;
        Displays = Desktop.Displays();
        ApplyPreferences(); Controller.Restore(State.Placement, Displays);
        _mascot = new(); MainWindow = _mascot; _hint = new(); _lyricBubble = new(); _notificationWindow = new();
        _mascot.Show(); CreateTray(); Render();
        Media.Changed += MediaChanged;
        if (!SmokeMode) _ = Media.InitializeAsync();
        if (!SmokeMode)
        {
            Microsoft.Win32.SystemEvents.SessionSwitch += SessionChanged;
            if (State.Preferences.NotificationsEnabled) _ = Notifications.ResumeAsync();
        }
        _clock.Start();
        _tick = new(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(40) };
        _tick.Tick += Tick; _tick.Start();
        if (_store.RecoveryNotice is { } notice) Notify(notice);
        if (SmokeMode)
        {
            Dispatcher.BeginInvoke(async () => await WindowsSmoke.RunAsync(this, _smokeOutput!), DispatcherPriority.ApplicationIdle);
        }
    }

    private void Unhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            var dir = _store is null ? Path.Combine(Path.GetTempPath(), "Maskot") : Path.GetDirectoryName(_store.FilePath)!;
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "errors.log"), DateTimeOffset.Now + "\n" + e.Exception + "\n");
            if (SmokeMode && _smokeOutput is not null) File.WriteAllText(Path.Combine(_smokeOutput, "failure.txt"), e.Exception.ToString());
        }
        catch { }
        if (!SmokeMode) MessageBox.Show("Maskot mengalami kesalahan. Detail tersimpan di %LOCALAPPDATA%\\Maskot\\errors.log. Aplikasi akan ditutup agar tidak berjalan dalam keadaan rusak.", "Maskot");
        e.Handled = true; Quit(1);
    }

    private void MediaChanged()
    {
        if (Exiting || Dispatcher.HasShutdownStarted) return;
        Dispatcher.BeginInvoke(() => { if (!Exiting) { RefreshKaraoke(); if (PanelVisible) _panel?.RefreshMedia(); Render(); } });
    }
    private void KaraokeChanged()
    {
        if (Exiting || Dispatcher.HasShutdownStarted) return;
        Dispatcher.BeginInvoke(() => { if (!Exiting) { if (PanelVisible) _panel?.RefreshLyrics(); Render(); } });
    }
    internal void RefreshKaraoke(bool force = false)
    {
        if (Karaoke is null) return;
        var m = ActiveMedia;
        Karaoke.Update(m is null ? null : new(m.Source, m.Title, m.Artist, m.Duration.TotalSeconds), State.Preferences.LyricsEnabled, force);
    }
    internal void SetKaraokeFixture(MediaView? media, LyricsDocument document, TimeSpan position)
    {
        if (!SmokeMode) throw new InvalidOperationException("Fixtures are restricted to --self-test.");
        _fixtureActive = true; _fixtureMedia = media; _fixturePosition = position; _fixtureLyrics!.Document = document;
        RefreshKaraoke(true); _panel?.RefreshMedia(); Render();
    }
    internal void SeekKaraokeFixture(TimeSpan position) { if (!SmokeMode) throw new InvalidOperationException(); _fixturePosition = position; Render(); }

    private void Tick(object? sender, EventArgs e)
    {
        var elapsed = _clock.Elapsed.TotalSeconds;
        var delta = elapsed - _lastTick; _lastTick = elapsed;
        var now = DateTimeOffset.UtcNow;
        if (Notifications.Advance(now, _notificationWindow?.IsMouseOver == true || Controller.Dragging || NotificationSettingsVisible || Controller.PanelOpen && !PanelVisible || _mascot?.IsVisible == false)) Render();
        if (!SmokeMode) AdvanceNotchHover(Desktop.Cursor, _clock.Elapsed);
        if (Controller.PointerHeld && Desktop.EscapeDown) _mascot?.CancelDrag();
        if (elapsed - _lastSecond >= 1)
        {
            _lastSecond = elapsed;
            if (!SmokeMode) Notifications.SuspendPresentation(_sessionLocked || !Desktop.AcceptsNotifications);
            Displays = Desktop.Displays(); Controller.ReconcileDisplays(Displays);
            var display = CurrentDisplay();
            var hidden = State.Preferences.HideInFullscreen && now >= _forceVisibleUntil && Desktop.FullscreenOn(display) && !PanelVisible && !Controller.PointerHeld;
            if (hidden) _mascot?.Hide(); else if (_mascot?.IsVisible == false) _mascot.Show();
            if (hidden) _lyricBubble?.Hide();
            if (hidden) _notificationWindow?.Hide();
            if (!hidden && !Controller.PanelOpen && _mascot is not null) Desktop.KeepAbove(_mascot);
            if (!hidden && !Controller.PanelOpen && _lyricBubble is not null) Desktop.KeepAbove(_lyricBubble);
            if (!hidden && PanelVisible && _panel is not null) Desktop.KeepAbove(_panel);
            if (!hidden && _notificationWindow is not null) Desktop.KeepAbove(_notificationWindow);
            if (State.Timer.Advance(now)) { SaveNow(); Notify(State.Timer.Phase == FocusPhase.Break ? "Sesi fokus selesai. Saatnya beristirahat. Buka panel untuk memulai istirahat." : "Istirahat selesai. Mulai sesi fokus saat kamu siap."); }
            _panel?.Tick();
        }
        if (_mascot?.IsVisible == true)
        {
            Controller.Tick(delta, SmokeMode ? new(-99999, -99999) : Desktop.Cursor, Displays); Render();
        }
        if (PanelVisible) _panel?.RefreshLyrics();
        if (_saveAt is { } due && now >= due || elapsed - _lastSave >= 15) SaveNow();
        var interval = Controller.PointerHeld || Controller.Mode == MascotMode.Walking && !Controller.PanelOpen && _mascot?.IsVisible == true ? 40 : Controller.Mode == MascotMode.Notch && (_peek.Pending || _previewPanel) ? 60 : ActiveMedia?.Playing == true && State.Preferences.LyricsEnabled ? 100 : 200;
        if (_tick is not null && _tick.Interval.TotalMilliseconds != interval) _tick.Interval = TimeSpan.FromMilliseconds(interval);
    }

    internal DisplayArea CurrentDisplay() => Displays.FirstOrDefault(d => d.Id == Controller.MonitorId) ?? DesktopGeometry.Nearest(Displays, Controller.Position);
    internal void ApplyPreferences()
    {
        Controller.Size = State.Preferences.Size; Controller.Speed = State.Preferences.WalkSpeed;
        Controller.ExtraAnimations = State.Preferences.ExtraAnimations;
        if (Displays.Count > 0) Controller.ReconcileDisplays(Displays);
        RefreshKaraoke();
        Render();
    }
    internal void Render()
    {
        if (_mascot is null || Displays.Count == 0) return;
        var cue = Cue;
        Controller.SetLyricNotch(cue.ExpandedNotch, Displays);
        var d = CurrentDisplay(); var (w, h) = Controller.Dimensions(d.Scale);
        _mascot.UpdateAppearance(cue); Desktop.Position(_mascot, Controller.Position, w, h);
        if (Controller.Dragging && Controller.DockNearby && Controller.DragDisplay is { } dragDisplay)
            _hint?.Update(dragDisplay, Controller.DockEligible);
        else _hint?.Hide();
        var notice = Notifications.Current ?? (_demoNotificationUntil > DateTimeOffset.UtcNow ? _demoNotification : null);
        var showNotice = notice is not null && _mascot.IsVisible && !Controller.Dragging && !_sessionLocked && !NotificationSettingsVisible && (!Controller.PanelOpen || PanelVisible);
        if (_mascot.IsVisible && Controller.Mode != MascotMode.Notch && cue.ShowBubble && !Controller.Dragging && !Controller.PanelOpen && !showNotice)
            _lyricBubble?.Update(cue, ActiveMedia?.Title ?? "", new(Controller.Position.X, Controller.Position.Y, w, h), d);
        else _lyricBubble?.Hide();
        if (PanelVisible) PositionPanel();
        if (showNotice)
        {
            var anchor = PanelVisible && Controller.Mode == MascotMode.Notch ? Desktop.WindowBounds(_panel!) : new RectD(Controller.Position.X, Controller.Position.Y, w, h);
            _notificationWindow?.Update(notice!, State.Preferences.NotificationHideContent, anchor, d, Controller.Mode == MascotMode.Notch);
        }
        else _notificationWindow?.ClearContents();
    }
    private void PositionPanel()
    {
        if (_panel is null) return;
        var d = CurrentDisplay(); var (w, h) = Controller.Dimensions(d.Scale);
        // Fit the actual physical workspace, including small displays and mixed DPI.
        var height = _panel.PreferredHeight;
        var width = _panel.PreferredWidth;
        var docked = Controller.Mode == MascotMode.Notch;
        _panel.ConfigureAnchor(docked);
        RectD target;
        if (docked) target = DesktopGeometry.NotchPanel(d, width, height);
        else
        {
            var scale = Math.Min(d.Scale, Math.Min(d.WorkArea.Width / (width + 12), d.WorkArea.Height / (height + 12)));
            var pw = width * scale; var ph = height * scale;
            var p = DesktopGeometry.PlacePanel(new(Controller.Position.X, Controller.Position.Y, w, h), d.WorkArea, pw, ph, 10 * scale);
            target = new(p.X, p.Y, pw, ph);
        }
        _panelMotion?.Move(target, State.Preferences.ExtraAnimations);
    }
    internal void WakeNotchHover()
    {
        if (SmokeMode || Controller.Mode != MascotMode.Notch) return;
        AdvanceNotchHover(Desktop.Cursor, _clock.Elapsed);
        if (_tick is not null && _peek.Pending) _tick.Interval = TimeSpan.FromMilliseconds(60);
    }
    internal void AdvanceNotchHover(PointD cursor, TimeSpan now)
    {
        var d = CurrentDisplay(); var (w, h) = Controller.Dimensions(d.Scale);
        var inside = new RectD(Controller.Position.X, Controller.Position.Y, w, h).Contains(cursor)
            || PanelVisible && Desktop.WindowBounds(_panel!).Contains(cursor);
        var action = _peek.Update(now, Controller.Mode == MascotMode.Notch && _mascot?.IsVisible == true
            && !Controller.PointerHeld && (PanelVisible || !Controller.PanelOpen), inside,
            PanelVisible, _previewPanel, _panel?.IsInteractionHeld == true);
        if (action == PeekAction.Open) OpenPanel(preview: true);
        else if (action == PeekAction.Close)
        {
            ClosePanel();
            _peek.Update(now, true, false, false, false, false);
        }
    }
    internal void TogglePanel() { if (PanelVisible) ClosePanel(); else OpenPanel(); }
    internal void RelayoutPanel() { if (PanelVisible) PositionPanel(); }
    internal void AddFiles(IEnumerable<string> paths) { OpenPanel(); _panel?.AddFiles(paths); }
    internal void OpenPanel(bool settings = false, bool preview = false)
    {
        _forceVisibleUntil = DateTimeOffset.UtcNow.AddSeconds(10);
        if (_panel is null) { _panel = new(); _panelMotion = new(_panel); }
        var wasVisible = PanelVisible;
        _previewPanel = preview;
        Controller.SetPanel(true);
        if (settings) _panel.OpenSettings();
        _panel.RefreshContent(); _panel.RefreshMedia();
        if (!wasVisible)
        {
            _panelMotion?.Reset();
            _panel.Show();
            if (Controller.Mode == MascotMode.Notch)
            {
                var d = CurrentDisplay(); var (w, h) = Controller.Dimensions(d.Scale);
                Desktop.Position(_panel, Controller.Position, w, h);
            }
        }
        PositionPanel(); if (!wasVisible) _panel.AnimateOpen(); if (!preview) _panel.Activate(); _panel.Tick(); Render();
    }
    internal void OpenLyricsPanel() { OpenPanel(); _panel?.ShowLyrics(); }
    internal void OpenNotificationsPanel() { OpenPanel(true); _panel?.ShowNotifications(); }
    internal async Task EnableNotificationsAsync()
    {
        await Notifications.EnableAsync();
        if (Exiting) return;
        State.Preferences.NotificationsEnabled = Notifications.IsEnabled;
        SaveSoon(); _panel?.RefreshNotifications(); Render();
    }
    internal void PauseNotifications()
    {
        Notifications.Disable(); _demoNotification = null;
        State.Preferences.NotificationsEnabled = false;
        SaveSoon(); _panel?.RefreshNotifications(); Render();
    }
    internal void DismissNotification() { Notifications.DismissCurrent(); _demoNotification = null; Render(); }
    internal void TestNotification()
    {
        _demoNotification = new(0, "momo.demo", "Momo · contoh", "Momo siap membawa kabar", "Ini hanya contoh tampilan. Notifikasi aplikasi akan muncul setelah akses Windows diizinkan.", DateTimeOffset.UtcNow);
        _demoNotificationUntil = DateTimeOffset.UtcNow.AddSeconds(8); ClosePanel(); Render();
    }
    internal async Task SetNotificationFixturesAsync(params DesktopNotice[] notices)
    {
        if (!SmokeMode || _fixtureNotifications is null) throw new InvalidOperationException("Notification fixtures require --self-test.");
        _fixtureNotifications.Snapshot = notices; await Notifications.RefreshAsync();
    }
    private void NotificationsChanged()
    {
        if (Exiting || Dispatcher.HasShutdownStarted) return;
        Dispatcher.BeginInvoke(() => { if (!Exiting) { _panel?.RefreshNotifications(); Render(); } });
    }
    private void SessionChanged(object sender, Microsoft.Win32.SessionSwitchEventArgs e)
    {
        if (Dispatcher.HasShutdownStarted) return;
        Dispatcher.BeginInvoke(() =>
        {
            if (Exiting) return;
            if (e.Reason == Microsoft.Win32.SessionSwitchReason.SessionLock) _sessionLocked = true;
            else if (e.Reason == Microsoft.Win32.SessionSwitchReason.SessionUnlock) _sessionLocked = false;
            Notifications.SuspendPresentation(_sessionLocked || !Desktop.AcceptsNotifications); _demoNotification = null; Render();
        });
    }
    internal void EnableLyrics()
    {
        State.Preferences.LyricsEnabled = true; RefreshKaraoke(); SaveNow(); _panel?.RefreshLyrics(); Render();
    }
    internal void ClosePanel()
    {
        _peek.Dismiss(); _previewPanel = false; _panelMotion?.Reset();
        if (_panel?.IsVisible == true) _panel.Hide();
        _panel?.ClearNotificationView();
        Controller.SetPanel(false); SaveSoon();
        Render();
    }
    internal void Dock()
    {
        ClosePanel();
        var d = CurrentDisplay();
        Controller.Restore(new(MascotMode.Notch, d.Bounds.Center, d.Id), Displays);
        SaveSoon(); Render();
    }
    internal void BeginNotchDrag(PointD press)
    {
        if (Controller.Mode != MascotMode.Notch) return;
        ClosePanel(); _mascot?.BeginDragFromPanel(press);
    }
    internal void Walk() { Controller.Walk(CurrentDisplay()); SaveSoon(); Render(); }
    internal void Stay() { Controller.Stay(); SaveSoon(); Render(); }
    internal void SaveSoon() => _saveAt = DateTimeOffset.UtcNow.AddMilliseconds(600);
    internal void SaveNow()
    {
        if (Controller.PointerHeld || _store is null) return;
        State.Placement = Controller.Snapshot();
        try
        {
            _store.Save(State); _saveAt = null; _lastSave = _clock.Elapsed.TotalSeconds; _lastSaveError = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (_lastSaveError != ex.Message) { _lastSaveError = ex.Message; Notify("Data belum dapat disimpan. Periksa ruang disk dan izin folder %LOCALAPPDATA%\\Maskot."); }
            _saveAt = DateTimeOffset.UtcNow.AddSeconds(15); _lastSave = _clock.Elapsed.TotalSeconds;
        }
    }

    private void CreateTray()
    {
        using var bitmap = new Drawing.Bitmap(32, 32);
        using (var g = Drawing.Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var cream = new Drawing.SolidBrush(Drawing.Color.FromArgb(247, 234, 208));
            using var dark = new Drawing.SolidBrush(Drawing.Color.FromArgb(73, 59, 48));
            g.FillPolygon(cream, new Drawing.Point[] { new(3, 15), new(3, 2), new(13, 8), new(23, 8), new(29, 2), new(29, 17) });
            g.FillEllipse(cream, 2, 6, 28, 24); g.FillEllipse(dark, 9, 16, 3, 4); g.FillEllipse(dark, 21, 16, 3, 4);
        }
        var handle = bitmap.GetHicon();
        using (var borrowed = Drawing.Icon.FromHandle(handle)) _icon = (Drawing.Icon)borrowed.Clone();
        Desktop.DestroyIcon(handle);
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Tampilkan Momo", null, (_, _) => { _forceVisibleUntil = DateTimeOffset.UtcNow.AddSeconds(10); _mascot?.Show(); Render(); });
        menu.Items.Add("Buka panel", null, (_, _) => OpenPanel());
        menu.Items.Add("Pengaturan", null, (_, _) => OpenPanel(true));
        menu.Items.Add("Lirik & karaoke", null, (_, _) => OpenLyricsPanel());
        menu.Items.Add("Notifikasi Windows", null, (_, _) => OpenNotificationsPanel());
        menu.Items.Add("Jalan-jalan", null, (_, _) => Walk());
        menu.Items.Add("Jadi notch", null, (_, _) => Dock());
        menu.Items.Add(new Forms.ToolStripSeparator()); menu.Items.Add("Keluar", null, (_, _) => Quit());
        menu.Opening += (_, _) => Controller.SetPanel(true);
        menu.Closed += (_, _) => { if (!PanelVisible) Controller.SetPanel(false); };
        _tray = new Forms.NotifyIcon { Icon = _icon, Text = "Maskot · Momo menemanimu", Visible = true, ContextMenuStrip = menu };
        _tray.DoubleClick += (_, _) => OpenPanel();
    }
    private void Notify(string text)
    {
        _panel?.Notice(text);
        if (_tray is not null) { _tray.BalloonTipTitle = "Maskot"; _tray.BalloonTipText = text; _tray.ShowBalloonTip(5000); }
    }
    internal void Quit() => Quit(0);
    internal void Quit(int exitCode)
    {
        if (Exiting) return;
        Exiting = true; _tick?.Stop(); _panelMotion?.Dispose(); Controller.CancelDrag();
        SaveNow(); Media.Changed -= MediaChanged; Media.Dispose();
        Microsoft.Win32.SystemEvents.SessionSwitch -= SessionChanged;
        if (Notifications is not null) { Notifications.Changed -= NotificationsChanged; Notifications.Dispose(); }
        if (Karaoke is not null) { Karaoke.Changed -= KaraokeChanged; Karaoke.Dispose(); }
        _lyricsClient?.Dispose();
        if (_tray is not null) { _tray.Visible = false; _tray.ContextMenuStrip?.Dispose(); _tray.Dispose(); }
        _icon?.Dispose();
        _panel?.Close(); _hint?.Close(); _lyricBubble?.Close(); _notificationWindow?.Close(); _mascot?.Close();
        _instance?.Dispose(); Shutdown(exitCode);
    }
    protected override void OnSessionEnding(SessionEndingCancelEventArgs e) { SaveNow(); base.OnSessionEnding(e); }
}
