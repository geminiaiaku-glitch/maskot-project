using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace Maskot.Windows.Platform;

internal sealed record MediaView(string Source, string Title, string Artist, BitmapImage? Cover,
    TimeSpan Position, TimeSpan Duration, bool Playing, bool CanPlayPause, bool CanPrevious, bool CanNext, bool CanSeek);

internal sealed record MediaMetadata(string Title, string Artist, BitmapImage? Cover);
internal sealed record MediaPlayback(TimeSpan Position, TimeSpan Duration, DateTimeOffset SampleAt, bool Playing,
    double Rate, bool CanPlayPause, bool CanPrevious, bool CanNext, bool CanSeek);

// The native adapter keeps WinRT separate from the refresh lifecycle, which is also exercised by self-test.
internal interface IMediaSession : IDisposable
{
    string Source { get; }
    event Action? MetadataChanged;
    event Action? PlaybackChanged;
    MediaPlayback ReadPlayback();
    Task<MediaMetadata> ReadMetadataAsync(CancellationToken cancellationToken);
    Task<bool> CommandAsync(string command, double seconds);
}

internal sealed class MediaService : IDisposable
{
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _nativeSession;
    private IMediaSession? _session;
    private MediaPlayback? _playback;
    private MediaMetadata? _metadata;
    private MetadataRefresh? _refresh;
    private string? _pinnedSource;
    private bool _initializing;
    private bool _disposed;
    private sealed class MetadataRefresh(IMediaSession session)
    {
        public IMediaSession Session { get; } = session;
        public CancellationTokenSource Cancellation { get; } = new();
        public int Revision { get; set; }
        public Task Task { get; set; } = Task.CompletedTask;
    }
    public MediaView? Current { get; private set; }
    public string Status { get; private set; } = "Putar lagu di Spotify, browser, atau pemutar favoritmu.";
    public IReadOnlyList<string> Sources
    {
        get
        {
            try { return _manager?.GetSessions().Select(s => s.SourceAppUserModelId).Distinct().ToArray() ?? []; }
            catch { return []; } // A player can exit while Windows enumerates it.
        }
    }
    public event Action? Changed;
    public TimeSpan Position => _playback is { } p
        ? PlaybackPosition.Estimate(p.Position, p.Duration, DateTimeOffset.UtcNow - p.SampleAt, p.Playing, p.Rate)
        : TimeSpan.Zero;

    private void Dispatch(Action action)
    {
        if (_dispatcher.HasShutdownStarted) return;
        if (_dispatcher.CheckAccess()) { if (!_disposed) action(); }
        else _dispatcher.BeginInvoke(() => { if (!_disposed) action(); });
    }
    public async Task InitializeAsync()
    {
        _dispatcher.VerifyAccess();
        if (_disposed || _initializing) return;
        if (_manager is not null) { Select(_pinnedSource); return; }
        _initializing = true;
        Status = "Menghubungkan media Windows…";
        try
        {
            var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            if (_disposed) return;
            _manager = manager;
            _manager.CurrentSessionChanged += ManagerChanged;
            _manager.SessionsChanged += SessionsChanged;
            Select(null);
        }
        catch (Exception) { if (!_disposed) { Status = "Integrasi media tidak tersedia. Coba kembali lewat tab Musik."; Changed?.Invoke(); } }
        finally { _initializing = false; }
    }
    private void ManagerChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args) => Dispatch(() =>
    {
        if (_pinnedSource is null || _session is null) Select(null);
    });
    private void SessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args) => Dispatch(() =>
    {
        try { if (_nativeSession is null || !sender.GetSessions().Contains(_nativeSession)) Select(null); }
        catch { Select(null); }
        Changed?.Invoke();
    });
    public void Select(string? source)
    {
        _dispatcher.VerifyAccess();
        if (_disposed) return;
        _pinnedSource = source;
        try
        {
            var next = source is null ? _manager?.GetCurrentSession() ?? _manager?.GetSessions().FirstOrDefault()
                : _manager?.GetSessions().FirstOrDefault(s => s.SourceAppUserModelId == source);
            if (Equals(_nativeSession, next) && _session is not null) { _ = RefreshAsync(); return; }
            _nativeSession = next;
            SetSession(next is null ? null : new WindowsMediaSession(next));
        }
        catch { SetSession(null); Status = "Sesi media belum dapat dibaca."; Changed?.Invoke(); }
    }
    internal void SetSession(IMediaSession? next)
    {
        _dispatcher.VerifyAccess();
        if (_disposed) { next?.Dispose(); return; }
        _refresh?.Cancellation.Cancel();
        _session?.Dispose();
        _session = next; _refresh = null; _playback = null; _metadata = null; Current = null;
        if (next is not null)
        {
            // Capture the sender: already queued callbacks from a former session must be ignored.
            next.MetadataChanged += () => Dispatch(() => { if (ReferenceEquals(_session, next)) _ = RefreshAsync(); });
            next.PlaybackChanged += () => Dispatch(() =>
            {
                if (!ReferenceEquals(_session, next)) return;
                if (_metadata is null && _refresh is null) _ = RefreshAsync();
                else RefreshPlayback(next);
            });
        }
        Status = next is null ? "Putar lagu di Spotify, browser, atau pemutar favoritmu." : "Menghubungkan media Windows…";
        Changed?.Invoke();
        if (next is not null) _ = RefreshAsync();
    }
    private void RefreshPlayback(IMediaSession session)
    {
        try
        {
            _playback = session.ReadPlayback();
            if (_metadata is { } metadata)
            {
                Current = View(session, metadata);
                Status = "";
            }
            Changed?.Invoke();
        }
        catch { _playback = null; Current = null; Status = "Sesi media belum dapat dibaca."; Changed?.Invoke(); }
    }
    private MediaView View(IMediaSession session, MediaMetadata metadata)
    {
        var p = _playback!;
        return new(session.Source, metadata.Title, metadata.Artist, metadata.Cover, Position, p.Duration,
            p.Playing, p.CanPlayPause, p.CanPrevious, p.CanNext, p.CanSeek && p.Duration > TimeSpan.Zero);
    }
    public Task RefreshAsync()
    {
        _dispatcher.VerifyAccess();
        if (_disposed || _session is null) return Task.CompletedTask;
        RefreshPlayback(_session);
        if (_refresh is { } active) { active.Revision++; return active.Task; }
        var refresh = new MetadataRefresh(_session);
        _refresh = refresh;
        refresh.Task = RefreshMetadataAsync(refresh);
        return refresh.Task;
    }
    private async Task RefreshMetadataAsync(MetadataRefresh refresh)
    {
        try
        {
            while (!_disposed && ReferenceEquals(_refresh, refresh))
            {
                var revision = refresh.Revision;
                MediaMetadata metadata;
                try { metadata = await refresh.Session.ReadMetadataAsync(refresh.Cancellation.Token); }
                catch
                {
                    if (_disposed || !ReferenceEquals(_refresh, refresh)) return;
                    if (revision != refresh.Revision) continue;
                    Current = null; _playback = null; _metadata = null; Status = "Sesi media belum dapat dibaca."; Changed?.Invoke(); return;
                }
                if (_disposed || !ReferenceEquals(_refresh, refresh)) return;
                if (revision != refresh.Revision) continue;
                // Playback is sampled again after the await; a slow cover must not restore stale Playing state.
                _playback = refresh.Session.ReadPlayback();
                _metadata = metadata; Current = View(refresh.Session, metadata); Status = ""; Changed?.Invoke(); return;
            }
        }
        catch
        {
            if (!_disposed && ReferenceEquals(_refresh, refresh))
            { Current = null; _playback = null; _metadata = null; Status = "Sesi media belum dapat dibaca."; Changed?.Invoke(); }
        }
        finally { if (ReferenceEquals(_refresh, refresh)) _refresh = null; refresh.Cancellation.Dispose(); }
    }
    public async Task CommandAsync(string command, double seconds = 0)
    {
        _dispatcher.VerifyAccess();
        var session = _session; var current = Current;
        if (_disposed || session is null || current is null) return;
        if (!(command switch { "play" => current.CanPlayPause, "previous" => current.CanPrevious,
            "next" => current.CanNext, "seek" => current.CanSeek && double.IsFinite(seconds), _ => false })) return;
        try
        {
            var ok = await session.CommandAsync(command, Math.Clamp(seconds, 0, current.Duration.TotalSeconds));
            if (!_disposed && ReferenceEquals(session, _session) && !ok)
            { Status = "Pemutar menolak perintah ini."; Changed?.Invoke(); }
        }
        catch
        {
            if (!_disposed && ReferenceEquals(session, _session))
            { Status = "Pemutar tidak merespons. Pilih sesi media kembali."; Changed?.Invoke(); }
        }
    }
    public void Dispose()
    {
        _dispatcher.VerifyAccess();
        if (_disposed) return;
        _disposed = true; _refresh?.Cancellation.Cancel(); _refresh = null;
        if (_manager is not null) { _manager.CurrentSessionChanged -= ManagerChanged; _manager.SessionsChanged -= SessionsChanged; }
        _session?.Dispose(); _session = null; Current = null; _playback = null; _metadata = null;
    }
}

internal sealed class WindowsMediaSession : IMediaSession
{
    private readonly GlobalSystemMediaTransportControlsSession _native;
    public string Source => _native.SourceAppUserModelId;
    public event Action? MetadataChanged;
    public event Action? PlaybackChanged;
    public WindowsMediaSession(GlobalSystemMediaTransportControlsSession session)
    {
        _native = session;
        session.MediaPropertiesChanged += PropertiesChanged;
        session.PlaybackInfoChanged += PlayingChanged;
        session.TimelinePropertiesChanged += TimelineChanged;
    }
    private void PropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args) => MetadataChanged?.Invoke();
    private void PlayingChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args) => PlaybackChanged?.Invoke();
    private void TimelineChanged(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs args) => PlaybackChanged?.Invoke();
    public MediaPlayback ReadPlayback()
    {
        var info = _native.GetPlaybackInfo(); var timeline = _native.GetTimelineProperties();
        var playing = info.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
        var rate = info.PlaybackRate is { } r && double.IsFinite(r) && r > 0 ? r : 1;
        var duration = timeline.EndTime > timeline.StartTime ? timeline.EndTime - timeline.StartTime : TimeSpan.Zero;
        var now = DateTimeOffset.UtcNow;
        var age = now - timeline.LastUpdatedTime;
        var position = PlaybackPosition.Estimate(timeline.Position - timeline.StartTime, duration,
            age >= TimeSpan.Zero && age < TimeSpan.FromDays(1) ? age : TimeSpan.Zero, playing, rate);
        var c = info.Controls;
        return new(position, duration, now, playing, rate, c.IsPlayPauseToggleEnabled, c.IsPreviousEnabled, c.IsNextEnabled, c.IsPlaybackPositionEnabled);
    }
    public async Task<MediaMetadata> ReadMetadataAsync(CancellationToken cancellationToken)
    {
        var props = await _native.TryGetMediaPropertiesAsync().AsTask(cancellationToken);
        BitmapImage? cover = null;
        if (props.Thumbnail is not null)
        {
            try
            {
                using var stream = await props.Thumbnail.OpenReadAsync().AsTask(cancellationToken);
                if (stream.Size is > 0 and <= 12 * 1024 * 1024)
                {
                    using var input = stream.GetInputStreamAt(0);
                    using var reader = new DataReader(input);
                    var bytes = new byte[(int)stream.Size];
                    await reader.LoadAsync((uint)bytes.Length).AsTask(cancellationToken); reader.ReadBytes(bytes);
                    using var memory = new MemoryStream(bytes);
                    cover = new(); cover.BeginInit(); cover.CacheOption = BitmapCacheOption.OnLoad;
                    cover.DecodePixelWidth = 240; cover.StreamSource = memory; cover.EndInit(); cover.Freeze();
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch { /* Missing/broken artwork must not prevent playback controls. */ }
        }
        return new(props.Title, props.Artist, cover);
    }
    public async Task<bool> CommandAsync(string command, double seconds) => command switch
    {
        "play" => await _native.TryTogglePlayPauseAsync(),
        "previous" => await _native.TrySkipPreviousAsync(),
        "next" => await _native.TrySkipNextAsync(),
        "seek" => await _native.TryChangePlaybackPositionAsync(_native.GetTimelineProperties().StartTime.Ticks + TimeSpan.FromSeconds(seconds).Ticks),
        _ => false
    };
    public void Dispose()
    {
        _native.MediaPropertiesChanged -= PropertiesChanged;
        _native.PlaybackInfoChanged -= PlayingChanged;
        _native.TimelinePropertiesChanged -= TimelineChanged;
        MetadataChanged = null; PlaybackChanged = null;
    }
}
