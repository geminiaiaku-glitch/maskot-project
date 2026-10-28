using Maskot.Windows.Platform;

namespace Maskot.Windows.Diagnostics;

internal static class NotificationServiceSmoke
{
    public static async Task RunAsync(Action<string, bool, object?> check)
    {
        var first = FixtureNotificationSource.Notice(1);
        var second = FixtureNotificationSource.Notice(2);
        var third = FixtureNotificationSource.Notice(3);
        var source = new FixtureNotificationSource { RequiresInstallation = true };
        using (var portable = new NotificationService(source, pollAutomatically: false))
        {
            await portable.EnableAsync(); await portable.ResumeAsync();
            check("notifications_portable_never_requests_permission_or_reads", portable.RequiresInstallation && !portable.IsEnabled
                && source.PermissionRequests == 0 && source.Reads == 0 && source.AccessChecks == 0, null);
        }
        source = new() { Access = NotificationAccess.Unspecified, Snapshot = [first] };
        using (var service = new NotificationService(source, pollAutomatically: false))
        {
            await service.ResumeAsync();
            check("notifications_resume_never_prompts_for_permission", !service.IsEnabled && source.PermissionRequests == 0 && source.Reads == 0, null);
            await service.EnableAsync();
            check("notifications_explicit_consent_baselines_existing_toasts", service.IsEnabled && source.PermissionRequests == 1
                && service.Current is null && service.Recent.Count == 0, null);
            source.Snapshot = [first, second]; await service.RefreshAsync();
            check("notifications_new_toast_enters_local_card", service.Current?.Id == second.Id && service.Recent.Count == 1, null);
            service.Clear(); await service.RefreshAsync();
            check("notifications_local_clear_does_not_replay_windows_snapshot", service.Current is null && service.Recent.Count == 0 && source.Snapshot.Count == 2, null);

            var gate = new TaskCompletionSource<IReadOnlyList<DesktopNotice>>(TaskCreationOptions.RunContinuationsAsynchronously);
            source.Responses.Enqueue(gate.Task);
            var reads = source.Reads; var pending = service.RefreshAsync();
            for (var i = 0; i < 30; i++) _ = service.RefreshAsync();
            check("notifications_overlapping_polls_coalesce", source.Reads == reads + 1 && !pending.IsCompleted, source.Reads - reads);
            service.Disable();
            var changes = 0; service.Changed += () => changes++;
            gate.SetResult([first, second, third]); await pending;
            check("notifications_disable_cancels_and_rejects_late_result", source.LastReadToken.IsCancellationRequested
                && !service.IsEnabled && service.Current is null && service.Recent.Count == 0 && changes == 0, null);

            await service.ResumeAsync();
            check("notifications_granted_resume_uses_fresh_baseline_without_prompt", service.IsEnabled && source.PermissionRequests == 1 && service.Current is null, null);
            gate = new(TaskCreationOptions.RunContinuationsAsynchronously); source.Responses.Enqueue(gate.Task);
            pending = service.RefreshAsync(); source.Access = NotificationAccess.Denied;
            gate.SetResult([first, second, third]); await pending;
            check("notifications_permission_revocation_purges_content_before_publish", !service.IsEnabled && service.Current is null && service.Recent.Count == 0, null);
            reads = source.Reads; await service.RefreshAsync();
            check("notifications_revoked_listener_stops_polling", source.Reads == reads, null);
        }
        source = new() { Snapshot = [first] };
        using (var service = new NotificationService(source, pollAutomatically: false))
        {
            await service.ResumeAsync(); source.Snapshot = [first, second]; await service.RefreshAsync();
            var gate = new TaskCompletionSource<IReadOnlyList<DesktopNotice>>(TaskCreationOptions.RunContinuationsAsynchronously);
            source.Responses.Enqueue(gate.Task); var stale = service.RefreshAsync();
            service.SuspendPresentation(true); source.Snapshot = [first, second, third];
            await service.RefreshAsync(); service.SuspendPresentation(false); await service.RefreshAsync();
            gate.SetResult([first]); await stale;
            check("notifications_lock_resume_discards_backlog_and_stale_baseline", service.Current is null && service.Recent.Count == 1, null);
            source.Snapshot = [first, second, third, FixtureNotificationSource.Notice(4)]; await service.RefreshAsync();
            check("notifications_after_unlock_present_only_fresh_toasts", service.Current?.Id == 4, null);
            source.Responses.Enqueue(Task.FromException<IReadOnlyList<DesktopNotice>>(new IOException("fixture transient read")));
            await service.RefreshAsync();
            check("notifications_transient_read_error_does_not_escape", service.IsEnabled, null);
            await service.RefreshAsync();
            check("notifications_transient_read_error_recovers_without_reprompt", service.IsEnabled && source.PermissionRequests == 0 && service.Recent.Count == 2, null);
        }
        source = new() { Access = NotificationAccess.Unspecified, RequestedAccess = NotificationAccess.Denied };
        using (var denied = new NotificationService(source, pollAutomatically: false))
        {
            await denied.EnableAsync();
            check("notifications_denied_permission_never_reads", !denied.IsEnabled && source.PermissionRequests == 1 && source.Reads == 0, null);
        }
        var permission = new TaskCompletionSource<NotificationAccess>(TaskCreationOptions.RunContinuationsAsynchronously);
        source = new() { Access = NotificationAccess.Unspecified, PermissionResponse = permission.Task };
        using (var disposed = new NotificationService(source, pollAutomatically: false))
        {
            var pending = disposed.EnableAsync(); disposed.Dispose();
            permission.SetResult(NotificationAccess.Allowed); await pending;
            check("notifications_disposal_rejects_late_permission_result", source.Disposed && source.LastPermissionToken.IsCancellationRequested
                && !disposed.IsEnabled && source.Reads == 0 && disposed.Current is null, null);
        }
    }
}

// Original synthetic messages only; App's --self-test can share this source without reading the desktop.
internal sealed class FixtureNotificationSource : INotificationSource
{
    public bool IsSupported { get; set; } = true;
    public bool RequiresInstallation { get; set; }
    public string? OwnSourceId { get; set; } = "test.momo";
    public NotificationAccess Access { get; set; } = NotificationAccess.Allowed;
    public NotificationAccess RequestedAccess { get; set; } = NotificationAccess.Allowed;
    public IReadOnlyList<DesktopNotice> Snapshot { get; set; } = [];
    public Task<NotificationAccess>? PermissionResponse { get; set; }
    public Queue<Task<IReadOnlyList<DesktopNotice>>> Responses { get; } = new();
    public int Reads { get; private set; }
    public int AccessChecks { get; private set; }
    public int PermissionRequests { get; private set; }
    public bool Disposed { get; private set; }
    public CancellationToken LastReadToken { get; private set; }
    public CancellationToken LastPermissionToken { get; private set; }
    public NotificationAccess GetAccessStatus() { AccessChecks++; return Access; }
    public async Task<NotificationAccess> RequestAccessAsync(CancellationToken cancellationToken)
    {
        PermissionRequests++; LastPermissionToken = cancellationToken;
        Access = PermissionResponse is { } response ? await response : RequestedAccess;
        return Access;
    }
    public Task<IReadOnlyList<DesktopNotice>> ReadAsync(CancellationToken cancellationToken)
    {
        Reads++; LastReadToken = cancellationToken;
        return Responses.TryDequeue(out var response) ? response : Task.FromResult(Snapshot);
    }
    public void Dispose() { Disposed = true; Snapshot = []; }
    public static DesktopNotice Notice(uint id) => new(id, "test.calendar", "Kalender uji", "Pengingat Momo", "Pesan buatan untuk pengujian antarmuka.",
        new DateTimeOffset(2026, 10, 28, 9, 0, 0, TimeSpan.Zero).AddSeconds(id));
}
