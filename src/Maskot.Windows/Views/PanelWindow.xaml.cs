using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Maskot.Windows.Platform;

namespace Maskot.Windows.Views;

public partial class PanelWindow : Window
{
    private App Host => (App)Application.Current;
    private bool _initializing = true, _refreshing, _seeking;
    private Point _shelfStart;
    private PointD? _headerPress;
    private ShelfItem? _shelfDrag;
    private bool _nativeDialog;
    private int _lastPage;
    private DateTimeOffset? _noticeUntil;
    private static readonly string[] PageNames = ["Musik", "Fokus", "Ruang", "Setelan"];
    private static readonly Brush[] PageAccents = [ColorBrush("#F083A0"), ColorBrush("#EFB68B"), ColorBrush("#83BDF2"), ColorBrush("#C7C7D2")];
    private static readonly Brush Muted = ColorBrush("#909097"), Dim = ColorBrush("#45454C"), Selected = ColorBrush("#29292F"), Dark = ColorBrush("#FE050506");
    private static readonly Brush LyricsBackground = CreateLyricsBackground();
    private static Brush ColorBrush(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)); brush.Freeze(); return brush;
    }
    private static Brush CreateLyricsBackground()
    {
        var brush = new LinearGradientBrush { StartPoint = new(0, 0), EndPoint = new(.4, 1) };
        brush.GradientStops.Add(new(Color.FromRgb(5, 5, 6), .35));
        brush.GradientStops.Add(new(Color.FromRgb(19, 32, 49), 1));
        brush.Freeze(); return brush;
    }
    public PanelWindow()
    {
        InitializeComponent();
        var state = Host.State; var prefs = state.Preferences;
        Notes.Text = state.Notes; FocusMinutes.Text = state.Timer.FocusMinutes.ToString(); BreakMinutes.Text = state.Timer.BreakMinutes.ToString();
        SizeSlider.Value = prefs.Size; SpeedSlider.Value = prefs.WalkSpeed; Animations.IsChecked = prefs.ExtraAnimations;
        Fullscreen.IsChecked = prefs.HideInFullscreen; OnlineLyrics.IsChecked = prefs.LyricsEnabled;
        HideNotificationContent.IsChecked = prefs.NotificationHideContent;
        Autostart.IsChecked = LoginStartup.IsEnabled();
        state.Preferences.StartAtLogin = Autostart.IsChecked == true;
        RefreshLists();
        _initializing = false;
        UpdateNavigation();
        IsVisibleChanged += (_, _) => { if (IsVisible) { UpdateNavigation(); Tick(); } };
        Closing += (_, e) => { if (!Host.Exiting) { e.Cancel = true; Host.ClosePanel(); } };
    }
    internal void OpenSettings() => Tabs.SelectedIndex = 3;
    internal void RefreshContent() => RefreshLists();
    internal bool LyricsExpanded => LyricsPane.Visibility == Visibility.Visible;
    internal bool IsInteractionHeld => _nativeDialog || _seeking || _headerPress is not null || Sources.IsDropDownOpen
        || IsKeyboardFocusWithin && Keyboard.FocusedElement is TextBoxBase;
    internal double PreferredWidth => Tabs.SelectedIndex == 0 && LyricsExpanded ? 620 : 420;
    internal double PreferredHeight => (Tabs.SelectedIndex switch
    {
        0 => (LyricsExpanded ? Host.State.Preferences.LyricsEnabled ? 300 : 330 : 282) + (SourcePicker.Visibility == Visibility.Visible ? 38 : 0),
        1 => 360, 2 => 500, _ => 526
    }) + (Status.Visibility == Visibility.Visible ? 34 : 0);
    internal void ConfigureAnchor(bool docked)
    {
        PanelShell.CornerRadius = docked ? new(0, 0, 26, 26) : new(26);
        UpdateLogicalSize();
    }
    private void UpdateLogicalSize() { PanelShell.Width = PreferredWidth; PanelShell.Height = PreferredHeight; }
    internal void SelectTab(int index) => Tabs.SelectedIndex = Math.Clamp(index, 0, PageNames.Length - 1);
    internal void Navigate(int delta) => SelectTab((Tabs.SelectedIndex + delta % PageNames.Length + PageNames.Length) % PageNames.Length);
    private void PreviousPageClicked(object sender, RoutedEventArgs e) => Navigate(-1);
    private void NextPageClicked(object sender, RoutedEventArgs e) => Navigate(1);
    private void PageClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string page } && int.TryParse(page, out var index)) SelectTab(index);
    }
    private void TabChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing || !ReferenceEquals(e.Source, Tabs)) return;
        var direction = Math.Sign(Tabs.SelectedIndex - _lastPage);
        UpdateNavigation(); Host.RelayoutPanel(); Tick(); RefreshLyrics();
        if (IsVisible) AnimatePage(direction);
    }
    private void UpdateNavigation()
    {
        var selected = Math.Clamp(Tabs.SelectedIndex, 0, PageNames.Length - 1);
        var buttons = new[] { NavMusic, NavFocus, NavSpace, NavSettings };
        var dots = new[] { DotMusic, DotFocus, DotSpace, DotSettings };
        for (var i = 0; i < buttons.Length; i++)
        {
            buttons[i].Foreground = i == selected ? PageAccents[i] : Muted;
            buttons[i].Background = i == selected ? Selected : Brushes.Transparent;
            System.Windows.Automation.AutomationProperties.SetItemStatus(buttons[i], i == selected ? "Halaman aktif" : "");
            dots[i].Width = i == selected ? 13 : 4; dots[i].Background = i == selected ? PageAccents[i] : Dim;
        }
        PageTitle.Text = PageNames[selected]; PageTitle.Foreground = PageAccents[selected];
        LyricsToggle.Foreground = LyricsExpanded ? PageAccents[2] : Muted;
        PanelShell.Background = selected == 0 && LyricsExpanded ? LyricsBackground : Dark;
        _lastPage = selected; UpdateLogicalSize();
    }
    internal void AnimateOpen()
    {
        ShellContent.BeginAnimation(OpacityProperty, null); OpenOffset.BeginAnimation(TranslateTransform.YProperty, null);
        if (!Host.State.Preferences.ExtraAnimations) return;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        ShellContent.BeginAnimation(OpacityProperty, new DoubleAnimation(.45, 1, TimeSpan.FromMilliseconds(150)));
        OpenOffset.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-5, 0, TimeSpan.FromMilliseconds(170)) { EasingFunction = ease });
    }
    private void AnimatePage(int direction)
    {
        PageSurface.BeginAnimation(OpacityProperty, null); PageOffset.BeginAnimation(TranslateTransform.XProperty, null);
        if (!Host.State.Preferences.ExtraAnimations) return;
        PageSurface.BeginAnimation(OpacityProperty, new DoubleAnimation(.4, 1, TimeSpan.FromMilliseconds(140)));
        PageOffset.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(direction * 16, 0, TimeSpan.FromMilliseconds(170))
        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
    }
    private void ToggleLyricsPanel(object sender, RoutedEventArgs e) => ToggleLyrics();
    internal void ToggleLyrics()
    {
        LyricsPane.Visibility = LyricsExpanded ? Visibility.Collapsed : Visibility.Visible;
        LyricsColumn.Width = LyricsExpanded ? new(1, GridUnitType.Star) : new(0);
        UpdateNavigation(); RefreshLyrics(); Host.RelayoutPanel();
        if (IsVisible) AnimatePage(0);
    }
    private void ToggleSources(object sender, RoutedEventArgs e)
    {
        SourcePicker.Visibility = SourcePicker.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        Host.RelayoutPanel();
    }
    private void HeaderPressed(object sender, MouseButtonEventArgs e)
    {
        if (Host.Controller.Mode != MascotMode.Notch || e.ChangedButton != MouseButton.Left) return;
        for (var node = e.OriginalSource as DependencyObject; node is not null && !ReferenceEquals(node, HeaderGrip);
            node = node is Visual ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node))
            if (node is ButtonBase) return;
        if (!HeaderGrip.CaptureMouse()) return;
        _headerPress = Desktop.Cursor; e.Handled = true;
    }
    private void HeaderMoved(object sender, MouseEventArgs e)
    {
        if (_headerPress is not { } press || e.LeftButton != MouseButtonState.Pressed) return;
        if ((Desktop.Cursor - press).Length < 6 * Host.CurrentDisplay().Scale) return;
        // Release this capture before handing the original press to the mascot's existing drag controller.
        _headerPress = null; HeaderGrip.ReleaseMouseCapture();
        Host.BeginNotchDrag(press); e.Handled = true;
    }
    private void HeaderReleased(object sender, MouseButtonEventArgs e)
    {
        if (_headerPress is null) return;
        _headerPress = null; HeaderGrip.ReleaseMouseCapture(); e.Handled = true;
    }
    private void HeaderCaptureLost(object sender, MouseEventArgs e) => _headerPress = null;
    private void CloseClicked(object sender, RoutedEventArgs e) => Host.ClosePanel();
    private void PanelKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { Host.ClosePanel(); e.Handled = true; return; }
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key is Key.Left or Key.Right
            && Keyboard.FocusedElement is not TextBoxBase && !Sources.IsDropDownOpen)
        { Navigate(e.Key == Key.Left ? -1 : 1); e.Handled = true; }
    }
    private void PanelDeactivated(object? sender, EventArgs e) { if (!_nativeDialog && !Sources.IsDropDownOpen) Host.ClosePanel(); }

    internal void RefreshMedia()
    {
        if (_initializing) return;
        _refreshing = true;
        try
        {
            var media = Host.ActiveMedia;
            var sources = Host.Media.Sources;
            if (!Sources.Items.Cast<string>().SequenceEqual(sources)) Sources.ItemsSource = sources;
            Sources.SelectedItem = media?.Source;
            TrackTitle.Text = media?.Title is { Length: > 0 } t ? t : "Belum ada musik";
            TrackArtist.Text = media?.Artist is { Length: > 0 } a ? a : "Putar lagu favoritmu.";
            Cover.Source = media?.Cover;
            PlayGlyph.Data = Geometry.Parse(media?.Playing == true ? "M0,0 L4,0 L4,14 L0,14 Z M8,0 L12,0 L12,14 L8,14 Z" : "M1,0 L12,7 L1,14 Z");
            PlayPause.IsEnabled = media?.CanPlayPause == true;
            Previous.IsEnabled = media?.CanPrevious == true; Next.IsEnabled = media?.CanNext == true;
            Seek.IsEnabled = media?.CanSeek == true;
            Seek.Maximum = Math.Max(1, media?.Duration.TotalSeconds ?? 1);
            Duration.Text = Format(media?.Duration ?? TimeSpan.Zero);
            MediaStatus.Text = Host.ActiveMediaStatus;
            MediaStatus.Visibility = string.IsNullOrWhiteSpace(MediaStatus.Text) ? Visibility.Collapsed : Visibility.Visible;
            TrackTitle.ToolTip = media?.Title; TrackArtist.ToolTip = media?.Artist;
            RefreshLyrics();
        }
        finally { _refreshing = false; }
    }
    private async void RefreshMediaClicked(object sender, RoutedEventArgs e)
    {
        if (Host.Media.Sources.Count == 0) await Host.Media.InitializeAsync(); else await Host.Media.RefreshAsync();
        Host.RefreshKaraoke(true); RefreshMedia();
    }
    private void SourceChanged(object sender, SelectionChangedEventArgs e) { if (!_refreshing && Sources.SelectedItem is string s) Host.Media.Select(s); }
    private async void PlayClicked(object sender, RoutedEventArgs e) => await Host.Media.CommandAsync("play");
    private async void PreviousClicked(object sender, RoutedEventArgs e) => await Host.Media.CommandAsync("previous");
    private async void NextClicked(object sender, RoutedEventArgs e) => await Host.Media.CommandAsync("next");
    private void SeekStarted(object sender, MouseButtonEventArgs e) => _seeking = true;
    private void SeekCaptureLost(object sender, MouseEventArgs e) { if (!Seek.IsMouseCaptureWithin) _seeking = false; }
    private async void SeekFinished(object sender, MouseButtonEventArgs e) { _seeking = false; await Host.Media.CommandAsync("seek", Seek.Value); }
    private async void SeekKeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Left or Key.Right or Key.Home or Key.End or Key.PageUp or Key.PageDown) await Host.Media.CommandAsync("seek", Seek.Value);
    }
    internal void Tick()
    {
        if (!IsVisible) return;
        PanelClock.Text = DateTime.Now.ToString("HH:mm"); PanelDate.Text = DateTime.Now.ToString("ddd, dd MMM");
        if (_noticeUntil is { } until && DateTimeOffset.UtcNow >= until)
        { _noticeUntil = null; Status.Visibility = Visibility.Collapsed; Host.RelayoutPanel(); }
        PanelBars.IsActive = Host.ActiveMedia?.Playing == true && Host.State.Preferences.ExtraAnimations;
        ModeStatus.Text = Host.Controller.Mode switch
        {
            MascotMode.Walking => "● Berjalan", MascotMode.Stationary => "● Diam di sini", _ => "● Notch"
        } + (Host.State.Preferences.HideInFullscreen ? " · sembunyi saat fullscreen" : " · selalu di atas");
        ModeAction.Content = Host.Controller.Mode == MascotMode.Walking ? "Diam" : "Jalan-jalan";
        if (!_seeking) Seek.Value = Host.ActivePosition.TotalSeconds;
        Elapsed.Text = Format(Host.ActivePosition);
        var timer = Host.State.Timer;
        Countdown.Text = Format(TimeSpan.FromSeconds(timer.Remaining(DateTimeOffset.UtcNow)));
        PhaseLabel.Text = timer.Phase == FocusPhase.Focus ? "WAKTU FOKUS" : "WAKTU ISTIRAHAT";
        StartPause.Content = timer.Running ? "Jeda" : timer.Phase == FocusPhase.Focus ? "Mulai fokus" : "Mulai istirahat";
        TimerStatus.Text = timer.Running ? "Momo menjaga waktu untukmu." : "Tarik napas. Mulai saat kamu siap.";
        FocusMinutes.IsEnabled = BreakMinutes.IsEnabled = !timer.Running;
        RefreshLyrics();
        if (Tabs.SelectedIndex == 3) RefreshNotifications();
    }
    internal void ShowNotifications()
    {
        OpenSettings(); RefreshNotifications();
        Dispatcher.BeginInvoke(() => NotificationSection.BringIntoView(new Rect(0, 0, NotificationSection.ActualWidth, 180)), System.Windows.Threading.DispatcherPriority.Loaded);
    }
    internal void RefreshNotifications()
    {
        if (_initializing) return;
        if (!IsVisible || Host.Exiting) { ClearNotificationView(); return; }
        NotificationStatus.Text = Host.Notifications.Status;
        EnableNotifications.IsEnabled = !_nativeDialog && !Host.Notifications.IsEnabled;
        PauseNotifications.IsEnabled = Host.Notifications.IsEnabled;
        HideNotificationContent.IsChecked = Host.State.Preferences.NotificationHideContent;
        NotificationHistory.ItemsSource = Host.Notifications.Recent.Select(n => new
        {
            Source = n.SourceName,
            Preview = Host.State.Preferences.NotificationHideContent ? "Notifikasi baru" : n.Title + (string.IsNullOrEmpty(n.Body) ? "" : " · " + n.Body)
        }).ToArray();
    }
    internal void ClearNotificationView() => NotificationHistory.ItemsSource = null;
    private async void EnableNotificationsClicked(object sender, RoutedEventArgs e)
    {
        _nativeDialog = true; EnableNotifications.IsEnabled = false;
        try { await Host.EnableNotificationsAsync(); }
        finally { _nativeDialog = false; if (!Host.Exiting) RefreshNotifications(); }
    }
    private void PauseNotificationsClicked(object sender, RoutedEventArgs e) => Host.PauseNotifications();
    private void TestNotificationClicked(object sender, RoutedEventArgs e) => Host.TestNotification();
    private void HideNotificationContentClicked(object sender, RoutedEventArgs e)
    {
        Host.State.Preferences.NotificationHideContent = HideNotificationContent.IsChecked == true;
        Host.SaveSoon(); RefreshNotifications(); Host.Render();
    }
    private void ClearNotificationHistoryClicked(object sender, RoutedEventArgs e) { Host.Notifications.Clear(); RefreshNotifications(); }
    private async void NotificationPermissionClicked(object sender, RoutedEventArgs e)
    {
        try { await global::Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:privacy-notifications")); }
        catch { Notice("Buka Pengaturan Windows → Privasi dan keamanan → Notifikasi."); }
    }
    private static string Format(TimeSpan t) => t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{(int)t.TotalMinutes:00}:{t.Seconds:00}";
    internal void RefreshLyrics()
    {
        if (_initializing) return;
        OnlineLyrics.IsChecked = Host.State.Preferences.LyricsEnabled;
        if (!IsVisible || Tabs.SelectedIndex != 0 || LyricsPane.Visibility != Visibility.Visible) return;
        var cue = Host.Cue;
        var doc = Host.Karaoke.Document;
        LyricText.Text = !Host.State.Preferences.LyricsEnabled || Host.ActiveMedia is null ? cue.Caption
            : doc.Availability == LyricsAvailability.Plain ? doc.Plain : cue.Current;
        PreviousLyric.Text = ""; NextLyric.Text = "";
        if (Host.State.Preferences.LyricsEnabled && Host.ActiveMedia is not null && doc.Availability == LyricsAvailability.Synced)
        {
            var index = LrcParser.CurrentIndex(doc.Lines, Host.ActivePosition);
            for (var i = index - 1; i >= 0; i--)
            {
                if (string.IsNullOrWhiteSpace(doc.Lines[i].Text)) continue;
                PreviousLyric.Text = doc.Lines[i].Text; break;
            }
            NextLyric.Text = cue.Next;
        }
        PreviousLyric.Visibility = string.IsNullOrEmpty(PreviousLyric.Text) ? Visibility.Collapsed : Visibility.Visible;
        NextLyric.Visibility = string.IsNullOrEmpty(NextLyric.Text) ? Visibility.Collapsed : Visibility.Visible;
        LyricStatus.Text = cue.Caption;
        LyricsConsent.Visibility = Host.State.Preferences.LyricsEnabled ? Visibility.Collapsed : Visibility.Visible;
        RetryLyrics.Visibility = Host.State.Preferences.LyricsEnabled && Host.ActiveMedia is not null ? Visibility.Visible : Visibility.Collapsed;
    }
    internal void ShowLyrics()
    {
        SelectTab(0); if (!LyricsExpanded) ToggleLyrics(); else { RefreshLyrics(); Host.RelayoutPanel(); }
    }
    private void EnableLyricsClicked(object sender, RoutedEventArgs e) { Host.EnableLyrics(); Host.RelayoutPanel(); }
    private void RetryLyricsClicked(object sender, RoutedEventArgs e) => Host.RefreshKaraoke(true);
    private void TimerToggle(object sender, RoutedEventArgs e)
    {
        var timer = Host.State.Timer;
        if (timer.Running) timer.Pause(DateTimeOffset.UtcNow); else timer.Start(DateTimeOffset.UtcNow);
        Host.SaveNow(); Tick();
    }
    private void TimerReset(object sender, RoutedEventArgs e) { Host.State.Timer.Reset(); Host.SaveNow(); Tick(); }
    private void TimerSettingsChanged(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        var timer = Host.State.Timer;
        if (timer.Running) return;
        if (int.TryParse(FocusMinutes.Text, out var focus) && int.TryParse(BreakMinutes.Text, out var rest) && focus is >= 1 and <= 180 && rest is >= 1 and <= 180)
        {
            if (timer.FocusMinutes == focus && timer.BreakMinutes == rest) return;
            timer.FocusMinutes = focus; timer.BreakMinutes = rest; timer.Reset(); Host.SaveSoon(); Tick();
        }
        else { FocusMinutes.Text = timer.FocusMinutes.ToString(); BreakMinutes.Text = timer.BreakMinutes.ToString(); Notice("Durasi harus 1–180 menit."); }
    }
    private void RefreshLists()
    {
        TaskList.ItemsSource = null; TaskList.ItemsSource = Host.State.Tasks;
        ShelfList.ItemsSource = null; ShelfList.ItemsSource = Host.State.Shelf;
    }
    private void ContentEdited(object sender, TextChangedEventArgs e)
    {
        if (_initializing) return;
        Host.State.Notes = Notes.Text; Host.SaveSoon();
    }
    private void AddTask(object sender, RoutedEventArgs e)
    {
        var text = NewTask.Text.Trim(); if (text.Length == 0) return;
        Host.State.Tasks.Add(new() { Text = text }); NewTask.Clear(); RefreshLists(); Host.SaveSoon();
    }
    private void TaskKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { AddTask(sender, e); e.Handled = true; } }
    private void TaskEdited(object sender, RoutedEventArgs e) { if (!_initializing) Host.SaveSoon(); }
    private void DeleteTask(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is TaskItem task) { Host.State.Tasks.Remove(task); RefreshLists(); Host.SaveSoon(); }
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
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;
        AddFiles(paths); e.Handled = true;
    }
    internal void AddFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if ((!File.Exists(path) && !Directory.Exists(path)) || Host.State.Shelf.Any(f => f.Path.Equals(path, StringComparison.OrdinalIgnoreCase))) continue;
            Host.State.Shelf.Add(new() { Path = path });
        }
        Tabs.SelectedIndex = 2; RefreshLists(); Host.SaveSoon(); Notice("Pintasan tersimpan. File asli tidak dipindahkan.");
    }
    private void ShelfDragStart(object sender, MouseButtonEventArgs e) { _shelfStart = e.GetPosition(this); _shelfDrag = (sender as FrameworkElement)?.Tag as ShelfItem; }
    private void ShelfDragMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _shelfDrag is null || (e.GetPosition(this) - _shelfStart).Length < 8) return;
        var item = _shelfDrag; _shelfDrag = null;
        if (!item.Exists) { Notice("File sudah dipindah atau dihapus."); return; }
        // Offer links/copies only; never offer Move or delete the original on completion.
        _nativeDialog = true;
        try { DragDrop.DoDragDrop(this, new DataObject(DataFormats.FileDrop, new[] { item.Path }), DragDropEffects.Copy | DragDropEffects.Link); }
        finally { _nativeDialog = false; }
    }
    private void OpenShelf(object sender, RoutedEventArgs e) => ShelfAction(sender, false);
    private void LocateShelf(object sender, RoutedEventArgs e) => ShelfAction(sender, true);
    private void ShelfAction(object sender, bool locate)
    {
        if ((sender as Button)?.Tag is not ShelfItem file) return;
        if (!file.Exists) { Notice("File sudah dipindah atau dihapus. Hapus pintasan lalu letakkan file dari lokasi barunya."); RefreshLists(); return; }
        try
        {
            if (locate && !Directory.Exists(file.Path)) Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + file.Path + "\"") { UseShellExecute = true });
            else Process.Start(new ProcessStartInfo(file.Path) { UseShellExecute = true });
        }
        catch { Notice("Windows tidak dapat membuka file ini. Periksa izin atau aplikasi default."); }
    }
    private void DeleteShelf(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is ShelfItem f) { Host.State.Shelf.Remove(f); RefreshLists(); Host.SaveSoon(); }
    }
    private void PreferenceChanged(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        var p = Host.State.Preferences;
        p.Size = SizeSlider.Value; p.WalkSpeed = SpeedSlider.Value; p.ExtraAnimations = Animations.IsChecked == true;
        p.HideInFullscreen = Fullscreen.IsChecked == true;
        Host.ApplyPreferences(); Host.SaveSoon();
    }
    private void AutostartChanged(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        try { LoginStartup.SetEnabled(Autostart.IsChecked == true); Host.State.Preferences.StartAtLogin = Autostart.IsChecked == true; Host.SaveSoon(); }
        catch (Exception ex) { Autostart.IsChecked = LoginStartup.IsEnabled(); Notice(ex.Message); }
    }
    private void LyricsChanged(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        Host.State.Preferences.LyricsEnabled = OnlineLyrics.IsChecked == true; Host.RefreshKaraoke(); RefreshLyrics(); Host.SaveSoon(); Host.RelayoutPanel();
    }
    private void WalkClicked(object sender, RoutedEventArgs e) { Host.Walk(); Host.ClosePanel(); }
    private void ModeActionClicked(object sender, RoutedEventArgs e)
    {
        if (Host.Controller.Mode == MascotMode.Walking) Host.Stay(); else Host.Walk();
        Host.ClosePanel();
    }
    private void StayClicked(object sender, RoutedEventArgs e) => Host.Stay();
    private void DockClicked(object sender, RoutedEventArgs e) => Host.Dock();
    private void QuitClicked(object sender, RoutedEventArgs e) => Host.Quit();
    internal void Notice(string message)
    {
        Status.Text = message; Status.ToolTip = message; Status.Visibility = Visibility.Visible;
        _noticeUntil = DateTimeOffset.UtcNow.AddSeconds(8); Host.RelayoutPanel();
    }
}
