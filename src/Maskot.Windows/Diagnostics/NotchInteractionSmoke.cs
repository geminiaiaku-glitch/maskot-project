using System.Windows;
using System.Windows.Controls;
using Maskot.Windows.Platform;

namespace Maskot.Windows.Diagnostics;

internal static class NotchInteractionSmoke
{
    public static async Task RunAsync(App app, string output, Action<string, bool, object?> check)
    {
        var song = new MediaView("fixture.peek", "Nada kecil Momo", "Momo Studio · uji", null,
            TimeSpan.Zero, TimeSpan.FromSeconds(40), true, false, false, false, false);
        app.State.Preferences.LyricsEnabled = true;
        app.SetKaraokeFixture(song, LyricsDocument.FromText("[00:00]Langkah kecil menemani hari\n[00:04]Nada ringan membawa cerita\n[00:08]Momo tersenyum di sini", null), TimeSpan.FromSeconds(5));
        app.Dock(); await Task.Delay(220);
        var d = app.CurrentDisplay();
        var anchor = app.Controller.Position + new PointD(120, 20);
        var outside = new PointD(d.WorkArea.X + 8, d.WorkArea.Bottom - 8);
        void Step(double ms, PointD cursor) => app.AdvanceNotchHover(cursor, TimeSpan.FromMilliseconds(ms));
        using var focus = new FocusWindow();
        focus.Show(); focus.Activate(); await Task.Delay(70);
        var foreground = Desktop.ForegroundHandle;
        Step(0, outside); Step(100, anchor); Step(220, anchor);
        check("notch_brief_hover_does_not_open", !app.PanelVisible, null);
        Step(300, anchor); await Task.Delay(250);
        var panel = app.PanelForDiagnostics!;
        panel.SelectTab(0); await Task.Delay(220);
        check("notch_hover_opens_without_activation", app.PanelVisible && Desktop.ForegroundHandle == foreground, null);
        var bounds = Desktop.WindowBounds(panel);
        check("expanded_notch_is_centered_at_monitor_top", Math.Abs(bounds.Y - d.Bounds.Y) <= 1 && Math.Abs(bounds.Center.X - d.Bounds.Center.X) <= 1, bounds);
        check("expanded_notch_hides_compact_surface", !app.MascotForDiagnostics.Notch.IsVisible, null);
        WindowsSmoke.Capture((FrameworkElement)panel.Content, Path.Combine(output, "notch-carousel-music.png"));
        Step(400, bounds.Center);
        check("hover_handoff_into_panel_stays_open", app.PanelVisible, null);
        panel.Navigate(1); await Task.Delay(230);
        check("carousel_next_selects_focus", panel.Tabs.SelectedIndex == 1, null);
        WindowsSmoke.Capture((FrameworkElement)panel.Content, Path.Combine(output, "notch-carousel-focus.png"));
        panel.StartPause.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check("carousel_focus_start_controls_real_timer", app.State.Timer.Running, null);
        panel.StartPause.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        panel.Navigate(1); await Task.Delay(230);
        panel.Notes.Text = "Catatan uji navigasi Momo";
        WindowsSmoke.Capture((FrameworkElement)panel.Content, Path.Combine(output, "notch-carousel-space.png"));
        panel.Navigate(1); await Task.Delay(230);
        WindowsSmoke.Capture((FrameworkElement)panel.Content, Path.Combine(output, "notch-carousel-settings.png"));
        panel.Navigate(1); await Task.Delay(230);
        check("carousel_wraps_forward", panel.Tabs.SelectedIndex == 0, null);
        panel.Navigate(-1); await Task.Delay(230);
        check("carousel_wraps_backward", panel.Tabs.SelectedIndex == 3, null);
        panel.SelectTab(2); await Task.Delay(230);
        check("carousel_preserves_notes_between_pages", panel.Notes.Text == app.State.Notes && app.State.Notes == "Catatan uji navigasi Momo", null);
        panel.SelectTab(0); panel.ShowLyrics(); await Task.Delay(250);
        var wide = Desktop.WindowBounds(panel);
        check("lyrics_expansion_grows_sideways_with_current_line", wide.Width > bounds.Width + 100 * d.Scale && panel.LyricText.Text == "Nada ringan membawa cerita", wide);
        WindowsSmoke.Capture((FrameworkElement)panel.Content, Path.Combine(output, "notch-carousel-lyrics.png"));
        panel.ToggleLyrics(); await Task.Delay(230);
        check("lyrics_can_return_to_compact_music_card", !panel.LyricsExpanded && Desktop.WindowBounds(panel).Width < wide.Width, null);
        Step(500, outside); Step(750, outside);
        check("notch_leave_has_grace_period", app.PanelVisible, null);
        Step(950, outside); await Task.Delay(100);
        check("notch_leave_collapses_back_to_lyrics", !app.PanelVisible && app.MascotForDiagnostics.Notch.IsVisible, null);
        Step(1050, anchor); Step(1250, anchor); await Task.Delay(220);
        check("notch_hover_reopens_after_leaving", app.PanelVisible, null);
        app.ClosePanel(); Step(1300, anchor); Step(2000, anchor);
        check("explicit_close_does_not_reopen_under_cursor", !app.PanelVisible, null);
        app.State.Preferences.ExtraAnimations = false; app.ApplyPreferences();
        Step(2100, outside); Step(2200, anchor); Step(2400, anchor); await Task.Delay(30);
        check("reduced_motion_still_opens_carousel", app.PanelVisible, null);
        app.BeginNotchDrag(Desktop.Cursor);
        app.Controller.MovePointer(Desktop.Cursor + new PointD(0, 100 * d.Scale), app.Displays); app.Render();
        check("expanded_header_drag_hands_back_to_momo", !app.PanelVisible && app.Controller.Dragging && app.Controller.Mode == MascotMode.Stationary && app.MascotForDiagnostics.Character.IsVisible, null);
        app.MascotForDiagnostics.CancelDrag(); app.Render();
        check("escape_after_header_drag_restores_notch", !app.Controller.PointerHeld && app.Controller.Mode == MascotMode.Notch && app.MascotForDiagnostics.Notch.IsVisible, null);
        app.ClosePanel(); app.State.Preferences.ExtraAnimations = true; app.ApplyPreferences();
    }
    private sealed class FocusWindow : Window, IDisposable
    {
        public FocusWindow() { Title = "Momo hover focus check"; Width = 120; Height = 80; ShowInTaskbar = false; }
        public void Dispose() => Close();
    }
}
