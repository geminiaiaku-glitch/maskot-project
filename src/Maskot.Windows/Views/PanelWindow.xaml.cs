using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Maskot.Windows.Platform;

namespace Maskot.Windows.Views;

public partial class PanelWindow : Window
{
    private App Host => (App)Application.Current;
    private bool _initializing = true, _refreshing, _seeking;
    private Point _shelfStart;
    private ShelfItem? _shelfDrag;
    private bool _nativeDialog;
    public PanelWindow()
    {
        InitializeComponent();
        var state = Host.State; var prefs = state.Preferences;
        Notes.Text = state.Notes; FocusMinutes.Text = state.Timer.FocusMinutes.ToString(); BreakMinutes.Text = state.Timer.BreakMinutes.ToString();
        SizeSlider.Value = prefs.Size; SpeedSlider.Value = prefs.WalkSpeed; Animations.IsChecked = prefs.ExtraAnimations;
        Fullscreen.IsChecked = prefs.HideInFullscreen; OnlineLyrics.IsChecked = prefs.LyricsEnabled;
        Autostart.IsChecked = LoginStartup.IsEnabled();
        state.Preferences.StartAtLogin = Autostart.IsChecked == true;
        RefreshLists();
        _initializing = false;
        Closing += (_, e) => { if (!Host.Exiting) { e.Cancel = true; Host.ClosePanel(); } };
    }
    internal void OpenSettings() => Tabs.SelectedIndex = 3;
    internal void RefreshContent() => RefreshLists();
    internal double PreferredHeight => Tabs.SelectedIndex switch
    {
        0 => 368 + (LyricsPane.Visibility == Visibility.Visible ? Host.State.Preferences.LyricsEnabled ? 170 : 210 : 0) + (SourcePicker.Visibility == Visibility.Visible ? 38 : 0),
        1 => 440, 2 => 560, _ => 580
    };
    internal void SelectTab(int index) => Tabs.SelectedIndex = index;
    private void TabChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initializing && ReferenceEquals(e.Source, Tabs)) Host.RelayoutPanel();
    }
    private void ToggleLyricsPanel(object sender, RoutedEventArgs e)
    {
        LyricsPane.Visibility = LyricsPane.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        Host.RelayoutPanel();
    }
    private void ToggleSources(object sender, RoutedEventArgs e)
    {
        SourcePicker.Visibility = SourcePicker.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        Host.RelayoutPanel();
    }
    private void CloseClicked(object sender, RoutedEventArgs e) => Host.ClosePanel();
    private void PanelKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) { Host.ClosePanel(); e.Handled = true; } }
    private void PanelDeactivated(object? sender, EventArgs e) { if (!_nativeDialog) Host.ClosePanel(); }

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
    private async void SeekFinished(object sender, MouseButtonEventArgs e) { _seeking = false; await Host.Media.CommandAsync("seek", Seek.Value); }
    private async void SeekKeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Left or Key.Right or Key.Home or Key.End or Key.PageUp or Key.PageDown) await Host.Media.CommandAsync("seek", Seek.Value);
    }
    internal void Tick()
    {
        if (!IsVisible) return;
        PanelClock.Text = DateTime.Now.ToString("HH:mm");
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
    }
    private static string Format(TimeSpan t) => t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{(int)t.TotalMinutes:00}:{t.Seconds:00}";
    internal void RefreshLyrics()
    {
        if (_initializing) return;
        var cue = Host.Cue;
        var doc = Host.Karaoke.Document;
        OnlineLyrics.IsChecked = Host.State.Preferences.LyricsEnabled;
        LyricText.Text = !Host.State.Preferences.LyricsEnabled || Host.ActiveMedia is null ? cue.Caption
            : doc.Availability == LyricsAvailability.Plain ? doc.Plain : cue.Current;
        LyricStatus.Text = cue.Caption;
        LyricsConsent.Visibility = Host.State.Preferences.LyricsEnabled ? Visibility.Collapsed : Visibility.Visible;
        RetryLyrics.Visibility = Host.State.Preferences.LyricsEnabled && Host.ActiveMedia is not null ? Visibility.Visible : Visibility.Collapsed;
    }
    internal void ShowLyrics()
    {
        Tabs.SelectedIndex = 0; LyricsPane.Visibility = Visibility.Visible; RefreshLyrics(); Host.RelayoutPanel();
    }
    private void EnableLyricsClicked(object sender, RoutedEventArgs e) => Host.EnableLyrics();
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
        Host.State.Preferences.LyricsEnabled = OnlineLyrics.IsChecked == true; Host.RefreshKaraoke(); RefreshLyrics(); Host.SaveSoon();
    }
    private void WalkClicked(object sender, RoutedEventArgs e) { Host.Walk(); Host.ClosePanel(); }
    private void ModeActionClicked(object sender, RoutedEventArgs e)
    {
        if (Host.Controller.Mode == MascotMode.Walking) Host.Stay(); else Host.Walk();
        Host.ClosePanel();
    }
    private void StayClicked(object sender, RoutedEventArgs e) => Host.Stay();
    private void QuitClicked(object sender, RoutedEventArgs e) => Host.Quit();
    internal void Notice(string message) { Status.Text = message; Status.ToolTip = message; }
}
