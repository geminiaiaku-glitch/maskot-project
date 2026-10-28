using System.Windows.Threading;
using Windows.ApplicationModel;
using Windows.Foundation.Metadata;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace Maskot.Windows.Platform;

internal enum NotificationAccess { Unspecified, Denied, Allowed }
internal interface INotificationSource : IDisposable
{
    bool IsSupported { get; }
    bool RequiresInstallation { get; }
    string? OwnSourceId { get; }
    NotificationAccess GetAccessStatus();
    Task<NotificationAccess> RequestAccessAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<DesktopNotice>> ReadAsync(CancellationToken cancellationToken);
}

/// <summary>UI-thread, permission-gated listener. Notification content exists only in this session's RAM.</summary>
internal sealed class NotificationService : IDisposable
{
    private const string InstalledMessage = "Versi portable hanya bisa mencoba tampilan kartu. Pasang paket Momo dengan dukungan notifikasi untuk mengaktifkan akses Windows.";
    private const string EnabledMessage = "Notifikasi Windows aktif · hanya disimpan sementara selama Momo berjalan.";
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private readonly INotificationSource _source;
    private readonly NotificationFeed _feed;
    private readonly DispatcherTimer _timer;
    private readonly bool _pollAutomatically;
    private Operation? _access, _poll;
    private int _revision;
    private bool _disposed;
    private sealed class Operation
    {
        public CancellationTokenSource Cancellation { get; } = new();
        public Task Task { get; set; } = Task.CompletedTask;
    }
    internal NotificationService(INotificationSource? source = null, bool pollAutomatically = true)
    {
        _source = source ?? new WindowsNotificationSource(); _pollAutomatically = pollAutomatically;
        _feed = new(_source.OwnSourceId);
        _timer = new(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(1500) };
        _timer.Tick += PollTick;
        Status = RequiresInstallation ? InstalledMessage : IsSupported ? "Notifikasi Windows belum diaktifkan." : "Notifikasi tidak didukung oleh Windows ini.";
    }
    public string Status { get; private set; }
    public bool IsEnabled { get; private set; }
    public bool IsSupported => _source.IsSupported;
    public bool RequiresInstallation => _source.RequiresInstallation;
    public IReadOnlyList<DesktopNotice> Recent => _feed.Recent;
    public DesktopNotice? Current => _feed.Current;
    public event Action? Changed;
    private void PollTick(object? sender, EventArgs args) => _ = RefreshAsync();
    public Task EnableAsync() => StartAsync(requestPermission: true);
    public Task ResumeAsync() => StartAsync(requestPermission: false);
    private Task StartAsync(bool requestPermission)
    {
        _dispatcher.VerifyAccess();
        if (_disposed) return Task.CompletedTask;
        if (_access is { } active) return active.Task;
        if (!IsSupported || RequiresInstallation)
        {
            Disable(); Status = RequiresInstallation ? InstalledMessage : "Notifikasi tidak didukung oleh Windows ini.";
            Changed?.Invoke(); return Task.CompletedTask;
        }
        if (IsEnabled) return RefreshAsync();
        var operation = new Operation(); _access = operation;
        var revision = ++_revision;
        operation.Task = StartCoreAsync(operation, revision, requestPermission);
        return operation.Task;
    }
    private async Task StartCoreAsync(Operation operation, int revision, bool requestPermission)
    {
        try
        {
            var access = _source.GetAccessStatus();
            // Only the explicit Enable button can display the Windows permission prompt.
            if (requestPermission && access != NotificationAccess.Allowed)
                access = await _source.RequestAccessAsync(operation.Cancellation.Token);
            if (!IsCurrent(revision)) return;
            if (access != NotificationAccess.Allowed) { AccessUnavailable(access); return; }
            IsEnabled = true; Status = EnabledMessage; Changed?.Invoke();
            if (!IsCurrent(revision) || !IsEnabled) return;
            if (_pollAutomatically && !_feed.IsSuspended) _timer.Start();
            await RefreshAsync();
        }
        catch (OperationCanceledException) when (operation.Cancellation.IsCancellationRequested) { }
        catch
        {
            if (IsCurrent(revision))
            { StopAndClear(); Status = "Izin notifikasi belum dapat dibaca. Coba aktifkan kembali melalui Setelan."; Changed?.Invoke(); }
        }
        finally { if (ReferenceEquals(_access, operation)) _access = null; operation.Cancellation.Dispose(); }
    }
    private bool IsCurrent(int revision) => !_disposed && revision == _revision;
    internal Task RefreshAsync()
    {
        _dispatcher.VerifyAccess();
        if (_disposed || !IsEnabled || _feed.IsSuspended) return Task.CompletedTask;
        if (_poll is { } active) return active.Task;
        var operation = new Operation(); _poll = operation;
        operation.Task = PollCoreAsync(operation, _revision);
        return operation.Task;
    }
    private async Task PollCoreAsync(Operation operation, int revision)
    {
        try
        {
            var access = _source.GetAccessStatus();
            if (access != NotificationAccess.Allowed) { AccessUnavailable(access); return; }
            var notices = await _source.ReadAsync(operation.Cancellation.Token);
            if (!IsCurrent(revision) || !ReferenceEquals(_poll, operation)) return;
            access = _source.GetAccessStatus();
            if (access != NotificationAccess.Allowed) { AccessUnavailable(access); return; }
            var changed = _feed.ApplySnapshot(notices, DateTimeOffset.UtcNow);
            if (Status != EnabledMessage) { Status = EnabledMessage; changed = true; }
            if (changed) Changed?.Invoke();
        }
        catch (OperationCanceledException) when (operation.Cancellation.IsCancellationRequested) { }
        catch
        {
            if (!IsCurrent(revision) || !ReferenceEquals(_poll, operation)) return;
            // A transient read error is retried; an inability to validate permission fails closed.
            try
            {
                var access = _source.GetAccessStatus();
                if (access != NotificationAccess.Allowed) { AccessUnavailable(access); return; }
                Status = "Windows belum dapat mengirim daftar notifikasi. Momo akan mencoba lagi."; Changed?.Invoke();
            }
            catch { AccessUnavailable(NotificationAccess.Unspecified); }
        }
        finally { if (ReferenceEquals(_poll, operation)) _poll = null; operation.Cancellation.Dispose(); }
    }
    private void AccessUnavailable(NotificationAccess access)
    {
        StopAndClear();
        Status = access == NotificationAccess.Denied
            ? "Akses notifikasi ditolak atau dicabut. Izinkan Momo melalui pengaturan privasi Windows, lalu aktifkan kembali."
            : "Izin notifikasi belum diberikan. Aktifkan untuk meminta izin Windows.";
        Changed?.Invoke();
    }
    private void StopAndClear()
    {
        _revision++; IsEnabled = false; _timer.Stop();
        var access = _access; var poll = _poll; _access = null; _poll = null;
        access?.Cancellation.Cancel(); poll?.Cancellation.Cancel();
        _feed.Reset();
    }
    public void Disable()
    {
        _dispatcher.VerifyAccess();
        if (_disposed) return;
        StopAndClear(); Status = "Notifikasi Windows dimatikan. Riwayat sementara telah dihapus."; Changed?.Invoke();
    }
    public bool Advance(DateTimeOffset now, bool hold)
    {
        _dispatcher.VerifyAccess();
        if (_disposed || !IsEnabled) return false;
        var changed = _feed.Advance(now, hold); if (changed) Changed?.Invoke(); return changed;
    }
    public void DismissCurrent()
    {
        _dispatcher.VerifyAccess();
        if (!_disposed && _feed.DismissCurrent(DateTimeOffset.UtcNow)) Changed?.Invoke();
    }
    public void Clear()
    {
        _dispatcher.VerifyAccess();
        if (!_disposed && _feed.Clear()) Changed?.Invoke();
    }
    public void SuspendPresentation(bool suspended)
    {
        _dispatcher.VerifyAccess();
        if (_disposed || !_feed.SuspendPresentation(suspended)) return;
        // Discard a read begun before the lock/quiet transition so it cannot establish a stale baseline.
        var poll = _poll; _poll = null; poll?.Cancellation.Cancel();
        if (suspended) _timer.Stop();
        else if (IsEnabled && _pollAutomatically) _timer.Start();
        Changed?.Invoke();
        if (IsEnabled) _ = RefreshAsync();
    }
    public void Dispose()
    {
        _dispatcher.VerifyAccess();
        if (_disposed) return;
        _disposed = true; StopAndClear(); _timer.Tick -= PollTick; _source.Dispose(); Changed = null;
    }
}

internal sealed class WindowsNotificationSource : INotificationSource
{
    private UserNotificationListener? _listener;
    public bool IsSupported { get; }
    public bool RequiresInstallation { get; }
    public string? OwnSourceId { get; }
    public WindowsNotificationSource()
    {
        IsSupported = ApiInformation.IsTypePresent("Windows.UI.Notifications.Management.UserNotificationListener");
        try { _ = Package.Current.Id.FamilyName; OwnSourceId = AppInfo.Current.AppUserModelId; }
        catch { RequiresInstallation = true; }
    }
    private UserNotificationListener Listener => _listener ??= UserNotificationListener.Current;
    public NotificationAccess GetAccessStatus() => Map(Listener.GetAccessStatus());
    public async Task<NotificationAccess> RequestAccessAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Map(await Listener.RequestAccessAsync().AsTask(cancellationToken));
    }
    private static NotificationAccess Map(UserNotificationListenerAccessStatus status) => status switch
    {
        UserNotificationListenerAccessStatus.Allowed => NotificationAccess.Allowed,
        UserNotificationListenerAccessStatus.Denied => NotificationAccess.Denied,
        _ => NotificationAccess.Unspecified
    };
    public async Task<IReadOnlyList<DesktopNotice>> ReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var items = await Listener.GetNotificationsAsync(NotificationKinds.Toast).AsTask(cancellationToken);
        var notices = new List<DesktopNotice>();
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var app = item.AppInfo;
                var source = app?.AppUserModelId ?? "windows.unknown";
                if (string.Equals(source, OwnSourceId, StringComparison.OrdinalIgnoreCase)) continue;
                var name = "Windows";
                try { name = app?.DisplayInfo.DisplayName ?? name; } catch { }
                var visual = item.Notification.Visual;
                var binding = visual.GetBinding(KnownNotificationBindings.ToastGeneric) ?? visual.Bindings.FirstOrDefault();
                var text = binding?.GetTextElements().Select(element => element.Text).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray() ?? [];
                notices.Add(new(item.Id, source, name, text.FirstOrDefault() ?? "Notifikasi baru",
                    string.Join("\n", text.Skip(1)), item.CreationTime.ToUniversalTime()));
            }
            catch { /* One removed/unsupported toast must not hide notifications from other apps. */ }
        }
        return notices;
    }
    public void Dispose() { _listener = null; }
}
