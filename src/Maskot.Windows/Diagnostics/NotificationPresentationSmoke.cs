using System.Text.Json;
using System.Windows;
using Maskot.Windows.Platform;

namespace Maskot.Windows.Diagnostics;

internal static class NotificationPresentationSmoke
{
    public static async Task RunAsync(App app, string output, Action<string, bool, object?> check)
    {
        app.ClosePanel(); app.Dock();
        var foreground = Desktop.ForegroundHandle;
        await app.EnableNotificationsAsync();
        check("notification_app_explicit_enable_uses_fixture_baseline", app.Notifications.IsEnabled && app.Notifications.Current is null, null);
        var notice = new DesktopNotice(101, "test.messages", "Pesan uji", "Kabar kecil untuk harimu", "Ini pesan contoh orisinal untuk memeriksa kartu Momo.", DateTimeOffset.UtcNow);
        await app.SetNotificationFixturesAsync(notice); await Task.Delay(100); app.Render();
        var card = app.NotificationForDiagnostics!;
        check("notification_new_message_renders_near_notch", card.IsVisible && card.Heading.Text == notice.Title && card.Message.Text == notice.Body, null);
        check("notification_card_does_not_activate_foreground", Desktop.ForegroundHandle == foreground, null);
        var bounds = Desktop.WindowBounds(card); var work = app.CurrentDisplay().WorkArea;
        check("notification_card_fits_work_area", bounds.X >= work.X && bounds.Right <= work.Right && bounds.Y >= work.Y && bounds.Bottom <= work.Bottom, bounds);
        WindowsSmoke.Capture((FrameworkElement)card.Content, Path.Combine(output, "notification-notch.png"));
        app.State.Preferences.NotificationHideContent = true; app.Render();
        check("notification_mask_hides_title_and_message", card.Heading.Text == "Notifikasi baru" && !card.Message.Text.Contains("orisinal"), null);
        WindowsSmoke.Capture((FrameworkElement)card.Content, Path.Combine(output, "notification-private.png"));
        app.State.Preferences.NotificationHideContent = false;
        app.Walk(); app.Stay(); app.Render(); await Task.Delay(220);
        check("notification_card_follows_momo", card.IsVisible && app.MascotForDiagnostics.Character.IsVisible, null);
        WindowsSmoke.Capture((FrameworkElement)card.Content, Path.Combine(output, "notification-momo.png"));
        app.OpenNotificationsPanel(); await Task.Delay(250);
        check("notification_settings_show_session_history", app.PanelForDiagnostics!.NotificationHistory.Items.Count == 1 && app.PanelForDiagnostics.NotificationStatus.Text.Contains("aktif"), null);
        WindowsSmoke.Capture((FrameworkElement)app.PanelForDiagnostics.Content, Path.Combine(output, "notification-settings.png"));
        app.ClosePanel();
        check("notification_hidden_panel_drops_projected_content", app.PanelForDiagnostics.NotificationHistory.ItemsSource is null, null);
        app.PauseNotifications(); await Task.Delay(50);
        check("notification_pause_clears_card_content_and_history", !card.IsVisible && card.Heading.Text == "" && card.Message.Text == "" && app.Notifications.Recent.Count == 0 && !app.State.Preferences.NotificationsEnabled, null);
        app.TestNotification(); await Task.Delay(50);
        check("notification_preview_works_without_windows_access", card.IsVisible && card.Source.Text.Contains("contoh") && !app.Notifications.IsEnabled, null);
        app.DismissNotification();
        check("notification_local_dismiss_clears_visible_text", !card.IsVisible && card.Source.Text == "" && card.Heading.Text == "", null);
        using var native = new WindowsNotificationSource();
        string? identity = null;
        try { identity = global::Windows.ApplicationModel.Package.Current.Id.FullName; } catch { }
        string? nativeAccess = null;
        if (identity is not null && native.IsSupported)
        {
            try { nativeAccess = native.GetAccessStatus().ToString(); }
            catch (Exception e) { nativeAccess = "Error:" + e.HResult.ToString("X8"); }
        }
        var probe = new { packageIdentity = identity, requiresInstallation = native.RequiresInstallation, listenerTypeAvailable = native.IsSupported, nativeAccess, userAccessRequested = false };
        File.WriteAllText(Path.Combine(output, "notification-platform.json"), JsonSerializer.Serialize(probe));
    }
}
