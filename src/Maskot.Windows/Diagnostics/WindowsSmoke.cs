using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Maskot.Windows.Platform;

namespace Maskot.Windows.Diagnostics;

// Runs the real WPF application on a Windows runner. No user data or media is loaded.
internal static class WindowsSmoke
{
    public static async Task RunAsync(App app, string output)
    {
        Directory.CreateDirectory(output);
        var checks = new List<object>();
        var exit = 0;
        Window? cover = null;
        void Check(string name, bool pass, object? evidence = null)
        {
            checks.Add(new { name, passed = pass, evidence });
            if (!pass) throw new InvalidOperationException("Windows UI regression: " + name);
        }
        try
        {
            await MediaServiceSmoke.RunAsync(Check);
            await Task.Delay(350);
            var mascot = app.MascotForDiagnostics;
            Check("character_loaded", mascot.Character.IsLoaded && mascot.Character.IsVisible);
            Check("fullscreen_hiding_disabled_by_default", !app.State.Preferences.HideInFullscreen);
            app.Walk(); app.ClosePanel();
            var first = Desktop.WindowBounds(mascot);
            var frameCount = mascot.Character.FramesRendered;
            var pixelsA = Capture(mascot.Character, Path.Combine(output, "walking-a.png"));
            await Task.Delay(310);
            var pixelsB = Capture(mascot.Character, Path.Combine(output, "walking-b.png"));
            var second = Desktop.WindowBounds(mascot);
            Check("native_window_walks", Math.Abs(second.X - first.X) >= 3, new { first, second });
            Check("animation_clock_advances", mascot.Character.FramesRendered > frameCount + 2);
            Check("walking_pixels_change", pixelsA != pixelsB);

            app.Stay(); await Task.Delay(120);
            var stillA = Desktop.WindowBounds(mascot);
            var idleA = Capture(mascot.Character, Path.Combine(output, "idle-a.png"));
            await Task.Delay(410);
            var idleB = Capture(mascot.Character, Path.Combine(output, "idle-b.png"));
            Check("stationary_position_preserved", Desktop.WindowBounds(mascot) == stillA);
            Check("stationary_character_still_animates", idleA != idleB);

            app.State.Preferences.ExtraAnimations = false; app.ApplyPreferences(); await Task.Delay(80);
            var reducedA = Capture(mascot.Character, Path.Combine(output, "reduced-a.png"));
            var reducedFrames = mascot.Character.FramesRendered;
            await Task.Delay(220);
            Check("reduced_motion_stops_frame_timer", mascot.Character.FramesRendered == reducedFrames);
            var reducedB = Capture(mascot.Character, Path.Combine(output, "reduced-b.png"));
            Check("animation_setting_stops_extra_motion", reducedA == reducedB);
            app.State.Preferences.ExtraAnimations = true; app.ApplyPreferences();

            var display = app.CurrentDisplay();
            cover = new Window { Title = "Maskot fullscreen regression surface", WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, Topmost = true, Background = Brushes.DarkSlateBlue };
            cover.Show(); Desktop.Position(cover, new(display.Bounds.X, display.Bounds.Y), display.Bounds.Width, display.Bounds.Height);
            cover.Activate();
            await Task.Delay(1550);
            Check("mascot_visible_over_fullscreen", mascot.IsVisible);
            Check("mascot_above_fullscreen_window", Desktop.IsAbove(mascot, cover));
            cover.Close(); cover = null;

            app.OpenPanel(); await Task.Delay(180);
            var panel = app.PanelForDiagnostics!;
            Check("panel_opens", panel.IsVisible);
            Capture((FrameworkElement)panel.Content, Path.Combine(output, "panel-music.png"));
            var panelIdleA = Capture(mascot.Character, Path.Combine(output, "panel-idle-a.png"));
            await Task.Delay(330);
            var panelIdleB = Capture(mascot.Character, Path.Combine(output, "panel-idle-b.png"));
            Check("open_panel_does_not_stop_character_animation", panelIdleA != panelIdleB);
            foreach (var tab in new[] { 1, 2, 3 })
            {
                panel.SelectTab(tab); app.RelayoutPanel(); await Task.Delay(120);
                Capture((FrameworkElement)panel.Content, Path.Combine(output, "panel-" + new[] { "music", "focus", "space", "settings" }[tab] + ".png"));
                if (tab == 1) Check("primary_button_text_has_contrast", HasReadablePrimaryText(panel.StartPause));
            }
            app.ClosePanel();
            app.Controller.Press(app.Controller.Position + new PointD(40, 40));
            app.Controller.MovePointer(new(display.Bounds.Center.X, display.Bounds.Y + 25 * display.Scale), app.Displays);
            app.Controller.Release(app.Displays); app.Render(); await Task.Delay(250);
            Check("docking_displays_notch", app.Controller.Mode == MascotMode.Notch && mascot.Notch.IsVisible);
            Capture((FrameworkElement)mascot.Content, Path.Combine(output, "notch.png"));
            Check("hidden_character_animation_clock_stops", !mascot.Character.IsVisible);
            var hiddenFrames = mascot.Character.FramesRendered; await Task.Delay(150);
            Check("no_hidden_animation_polling", mascot.Character.FramesRendered == hiddenFrames);

            // Original test lyrics only; never capture real user media or copyrighted song text.
            app.State.Preferences.LyricsEnabled = true;
            var song = new MediaView("test.karaoke", "Uji Karaoke Momo", "Momo Studio · uji", null,
                TimeSpan.Zero, TimeSpan.FromSeconds(12), true, false, false, false, false);
            var lyrics = LyricsDocument.FromText("[00:00.00]Langkah kecil menemani hari\n[00:03.00]Nada ringan membawa cerita\n[00:07.00]Momo tersenyum di sini", null);
            app.SetKaraokeFixture(song, lyrics, TimeSpan.FromSeconds(1)); await Task.Delay(150);
            Check("lyric_notch_is_a_compact_pill", Desktop.WindowBounds(mascot).Width >= 350 && Desktop.WindowBounds(mascot).Height is >= 40 and <= 50);
            Check("notch_displays_active_lyric", mascot.NotchLyricLine.Text == "Langkah kecil menemani hari");
            Capture((FrameworkElement)mascot.Content, Path.Combine(output, "karaoke-notch.png"));
            app.SeekKaraokeFixture(TimeSpan.FromSeconds(5)); await Task.Delay(100);
            Check("notch_lyrics_follow_seek", mascot.NotchLyricLine.Text == "Nada ringan membawa cerita");
            app.SeekKaraokeFixture(TimeSpan.FromSeconds(1)); await Task.Delay(80);
            Check("notch_lyrics_follow_rewind", mascot.NotchLyricLine.Text == "Langkah kecil menemani hari");

            var lookups = app.FixtureLyricsRequests;
            app.Walk(); app.Stay(); app.Render(); await Task.Delay(160);
            Check("mode_change_keeps_lyrics_without_refetch", app.FixtureLyricsRequests == lookups && app.Cue.Current == "Langkah kecil menemani hari");
            Check("momo_holds_microphone", mascot.Character.Microphone.IsVisible);
            Check("momo_singing_mouth_visible", mascot.Character.SingingMouth.IsVisible && mascot.Character.Performance == KaraokeMood.Singing);
            Check("lyric_bubble_visible", app.BubbleForDiagnostics?.IsVisible == true && app.BubbleForDiagnostics.CurrentLine == "Langkah kecil menemani hari");
            Check("lyric_bubble_does_not_capture_clicks_or_focus", Desktop.IsPassiveClickThrough(app.BubbleForDiagnostics!));
            var singingA = Capture(mascot.Character, Path.Combine(output, "momo-singing-a.png"));
            Capture((FrameworkElement)app.BubbleForDiagnostics!.Content, Path.Combine(output, "momo-lyric-bubble.png"));
            await Task.Delay(230);
            var singingB = Capture(mascot.Character, Path.Combine(output, "momo-singing-b.png"));
            Check("singing_pixels_animate", singingA != singingB);

            app.SetKaraokeFixture(song, LyricsDocument.Empty(LyricsAvailability.Missing), TimeSpan.FromSeconds(5)); await Task.Delay(120);
            Check("missing_lyrics_make_momo_confused", mascot.Character.ConfusedBrows.IsVisible && mascot.Character.ConfusedMouth.IsVisible && mascot.Character.Microphone.IsVisible && mascot.Character.Performance == KaraokeMood.Confused);
            Check("confusion_uses_gestures_without_text", !HasVisibleText(mascot.Character) && app.BubbleForDiagnostics?.IsVisible == false && string.IsNullOrEmpty(app.Cue.Current) && string.IsNullOrEmpty(app.Cue.Next));
            var confusedA = Capture(mascot.Character, Path.Combine(output, "momo-confused.png"));
            await Task.Delay(250);
            var confusedB = Capture(mascot.Character, Path.Combine(output, "momo-confused-b.png"));
            Check("confused_gestures_animate", confusedA != confusedB);
            app.Controller.Press(app.Controller.Position + new PointD(40, 40));
            app.Controller.MovePointer(new(display.Bounds.Center.X, display.Bounds.Y + 25 * display.Scale), app.Displays);
            app.Controller.Release(app.Displays); app.Render(); await Task.Delay(220);
            Check("missing_lyrics_leave_notch_compact_without_status_text", mascot.Notch.IsVisible && mascot.NotchLyrics.Visibility == Visibility.Collapsed && Desktop.WindowBounds(mascot).Width <= 240 && Desktop.WindowBounds(mascot).Height <= 50);
            Capture((FrameworkElement)mascot.Content, Path.Combine(output, "notch-without-lyrics.png"));
            app.Walk(); app.Stay(); app.Render(); await Task.Delay(220);
            app.SetKaraokeFixture(song with { Playing = false }, LyricsDocument.Empty(LyricsAvailability.Missing), TimeSpan.FromSeconds(5)); await Task.Delay(80);
            Check("pause_clears_confused_face_and_microphone", !mascot.Character.ConfusedBrows.IsVisible && !mascot.Character.ConfusedMouth.IsVisible && !mascot.Character.Microphone.IsVisible && app.BubbleForDiagnostics?.IsVisible == false);
            Capture(mascot.Character, Path.Combine(output, "momo-paused.png"));
            app.SetKaraokeFixture(song, LyricsDocument.Empty(LyricsAvailability.Missing), TimeSpan.FromSeconds(5)); await Task.Delay(80);
            Check("resume_restores_confused_face_without_speech", mascot.Character.ConfusedMouth.IsVisible && mascot.Character.Microphone.IsVisible && app.BubbleForDiagnostics?.IsVisible == false);
            app.SetKaraokeFixture(song, LyricsDocument.Empty(LyricsAvailability.Error), TimeSpan.FromSeconds(5)); await Task.Delay(80);
            Check("lyric_connection_error_is_handled", mascot.Character.ConfusedMouth.IsVisible && app.Cue.Mood == KaraokeMood.Confused && app.BubbleForDiagnostics?.IsVisible == false);
            app.SetKaraokeFixture(song, LyricsDocument.Empty(LyricsAvailability.Instrumental), TimeSpan.FromSeconds(5)); await Task.Delay(80);
            Check("instrumental_uses_confused_face_without_speech", mascot.Character.ConfusedMouth.IsVisible && mascot.Character.Microphone.IsVisible && app.BubbleForDiagnostics?.IsVisible == false);
            app.SetKaraokeFixture(song, LyricsDocument.Empty(LyricsAvailability.Loading), TimeSpan.FromSeconds(5)); await Task.Delay(80);
            Check("loading_holds_microphone_without_speech", mascot.Character.Microphone.IsVisible && !mascot.Character.ConfusedMouth.IsVisible && app.BubbleForDiagnostics?.IsVisible == false);

            app.SetKaraokeFixture(song with { Playing = false }, lyrics, TimeSpan.FromSeconds(5)); await Task.Delay(80);
            Check("pause_stops_singing_microphone_and_bubble", !mascot.Character.SingingMouth.IsVisible && !mascot.Character.Microphone.IsVisible && app.BubbleForDiagnostics?.IsVisible == false);
            app.SetKaraokeFixture(song, lyrics, TimeSpan.FromSeconds(5)); await Task.Delay(80);
            Check("resume_restores_singing_microphone_and_lyrics", mascot.Character.SingingMouth.IsVisible && mascot.Character.Microphone.IsVisible && app.BubbleForDiagnostics?.IsVisible == true && app.BubbleForDiagnostics.CurrentLine == "Nada ringan membawa cerita");
            app.SetKaraokeFixture(null, LyricsDocument.Empty(LyricsAvailability.None), TimeSpan.Zero); await Task.Delay(80);
            Check("no_media_removes_microphone", !mascot.Character.Microphone.IsVisible && app.BubbleForDiagnostics?.IsVisible == false);
            app.State.Preferences.LyricsEnabled = false;
            lookups = app.FixtureLyricsRequests;
            app.SetKaraokeFixture(song, lyrics, TimeSpan.Zero); await Task.Delay(80);
            Check("lyrics_opt_in_is_respected", app.FixtureLyricsRequests == lookups && mascot.Character.Microphone.IsVisible && app.BubbleForDiagnostics?.IsVisible == false);
            Capture(mascot.Character, Path.Combine(output, "momo-music-without-online-lyrics.png"));
            app.SetKaraokeFixture(song with { Playing = false }, lyrics, TimeSpan.Zero); await Task.Delay(80);
            Check("pause_removes_microphone_with_lyrics_disabled", app.FixtureLyricsRequests == lookups && !mascot.Character.Microphone.IsVisible && app.BubbleForDiagnostics?.IsVisible == false);

            await NotchInteractionSmoke.RunAsync(app, output, Check);
            await ProbeLyricsProviderAsync(output);
        }
        catch (Exception e)
        {
            exit = 1; checks.Add(new { error = e.ToString() });
            File.WriteAllText(Path.Combine(output, "failure.txt"), e.ToString());
        }
        finally
        {
            cover?.Close();
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new
            {
                passed = exit == 0, platform = Environment.OSVersion.ToString(), finishedUtc = DateTimeOffset.UtcNow,
                scope = "WPF rendering, window movement, fullscreen layer, karaoke fixtures and timeline; real player integration still requires manual testing",
                checks
            }, new JsonSerializerOptions { WriteIndented = true }));
            app.Quit(exit);
        }
    }
    private static bool HasVisibleText(DependencyObject node)
    {
        if (node is TextBlock text && text.IsVisible && text.Opacity > 0 && !string.IsNullOrWhiteSpace(text.Text)) return true;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            if (HasVisibleText(VisualTreeHelper.GetChild(node, i))) return true;
        return false;
    }
    private static async Task ProbeLyricsProviderAsync(string output)
    {
        // Public test metadata only. Store availability/counts, never the returned song lyrics.
        using var client = new LyricsClient();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(23));
        object result;
        try
        {
            var doc = await client.FetchAsync(new("diagnostic", "Never Gonna Give You Up", "Rick Astley", 213), timeout.Token);
            result = new { reachable = doc.Availability != LyricsAvailability.Error, availability = doc.Availability.ToString(), timedLineCount = doc.Lines.Count, plainLyricsAvailable = doc.Plain.Length > 0 };
        }
        catch (Exception e) { result = new { reachable = false, failure = e.GetType().Name }; }
        File.WriteAllText(Path.Combine(output, "provider-probe.json"), JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    }

    internal static string Capture(FrameworkElement visual, string path)
    {
        visual.UpdateLayout();
        var width = Math.Max(1, (int)Math.Ceiling(visual.ActualWidth));
        var height = Math.Max(1, (int)Math.Ceiling(visual.ActualHeight));
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var file = File.Create(path)) encoder.Save(file);
        var pixels = new byte[width * height * 4]; bitmap.CopyPixels(pixels, width * 4, 0);
        return Convert.ToHexString(SHA256.HashData(pixels));
    }
    private static bool HasReadablePrimaryText(Button button)
    {
        static TextBlock? Text(DependencyObject node)
        {
            if (node is TextBlock text) return text;
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
                if (Text(VisualTreeHelper.GetChild(node, i)) is { } child) return child;
            return null;
        }
        static double Luminance(Color c)
        {
            static double Linear(byte channel) { var v = channel / 255d; return v <= .04045 ? v / 12.92 : Math.Pow((v + .055) / 1.055, 2.4); }
            return .2126 * Linear(c.R) + .7152 * Linear(c.G) + .0722 * Linear(c.B);
        }
        if (Text(button)?.Foreground is not SolidColorBrush foreground || button.Background is not SolidColorBrush background) return false;
        var a = Luminance(foreground.Color); var b = Luminance(background.Color);
        return (Math.Max(a, b) + .05) / (Math.Min(a, b) + .05) >= 4.5;
    }
}
