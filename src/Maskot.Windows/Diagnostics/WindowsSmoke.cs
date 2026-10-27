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
            await Task.Delay(220);
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
                scope = "WPF rendering, actual window movement, fullscreen Z-order, panel layout; not an interactive Windows 11 acceptance test",
                checks
            }, new JsonSerializerOptions { WriteIndented = true }));
            app.Quit(exit);
        }
    }

    private static string Capture(FrameworkElement visual, string path)
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
