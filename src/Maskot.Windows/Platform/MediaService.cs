using System.Windows.Media.Imaging;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace Maskot.Windows.Platform;

internal sealed record MediaView(string Source, string Title, string Artist, BitmapImage? Cover,
    TimeSpan Position, TimeSpan Duration, bool Playing, bool CanPlayPause, bool CanPrevious, bool CanNext, bool CanSeek);

internal sealed class MediaService : IDisposable
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private int _revision;
    private TimeSpan _position, _duration;
    private DateTimeOffset _sampleAt;
    private bool _playing;
    private double _playbackRate = 1;
    private string? _pinnedSource;
    private bool _initializing;
    private bool _disposed;
    public MediaView? Current { get; private set; }
    public string Status { get; private set; } = "Putar lagu di Spotify, browser, atau pemutar favoritmu.";
    public IReadOnlyList<string> Sources => _manager?.GetSessions().Select(s => s.SourceAppUserModelId).Distinct().ToArray() ?? [];
    public event Action? Changed;
    public TimeSpan Position
    {
        get
        {
            return PlaybackPosition.Estimate(_position, _duration, DateTimeOffset.UtcNow - _sampleAt, _playing, _playbackRate);
        }
    }

    public async Task InitializeAsync()
    {
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
        catch (Exception) { Status = "Integrasi media tidak tersedia. Coba kembali lewat tab Musik."; Changed?.Invoke(); }
        finally { _initializing = false; }
    }
    private void ManagerChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
    {
        // Keep a user-selected source while it is available.
        if (_pinnedSource is null || _session is null) Select(null);
    }
    private void SessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args)
    {
        if (_session is null || !sender.GetSessions().Contains(_session)) Select(null);
        Changed?.Invoke();
    }
    public void Select(string? source)
    {
        if (_disposed) return;
        _pinnedSource = source;
        var next = source is null ? _manager?.GetCurrentSession() ?? _manager?.GetSessions().FirstOrDefault()
            : _manager?.GetSessions().FirstOrDefault(s => s.SourceAppUserModelId == source);
        if (_session is not null)
        {
            _session.MediaPropertiesChanged -= PropertiesChanged;
            _session.PlaybackInfoChanged -= PlaybackChanged;
            _session.TimelinePropertiesChanged -= TimelineChanged;
        }
        _session = next;
        if (_session is not null)
        {
            _session.MediaPropertiesChanged += PropertiesChanged;
            _session.PlaybackInfoChanged += PlaybackChanged;
            _session.TimelinePropertiesChanged += TimelineChanged;
        }
        _ = RefreshAsync();
    }
    private void PropertiesChanged(GlobalSystemMediaTransportControlsSession s, MediaPropertiesChangedEventArgs a) => _ = RefreshAsync();
    private void PlaybackChanged(GlobalSystemMediaTransportControlsSession s, PlaybackInfoChangedEventArgs a) => _ = RefreshAsync();
    private void TimelineChanged(GlobalSystemMediaTransportControlsSession s, TimelinePropertiesChangedEventArgs a) => _ = RefreshAsync();

    public async Task RefreshAsync()
    {
        var version = Interlocked.Increment(ref _revision);
        var session = _session;
        if (session is null)
        {
            Current = null; _playing = false; Status = "Putar lagu di Spotify, browser, atau pemutar favoritmu."; Changed?.Invoke(); return;
        }
        try
        {
            var info = session.GetPlaybackInfo();
            var timeline = session.GetTimelineProperties();
            var props = await session.TryGetMediaPropertiesAsync();
            BitmapImage? cover = null;
            if (props.Thumbnail is not null)
            {
                try
                {
                    using var stream = await props.Thumbnail.OpenReadAsync();
                    using var reader = new DataReader(stream.GetInputStreamAt(0));
                    if (stream.Size <= 12 * 1024 * 1024)
                    {
                        var bytes = new byte[(int)stream.Size];
                        await reader.LoadAsync((uint)bytes.Length); reader.ReadBytes(bytes);
                        using var memory = new MemoryStream(bytes);
                        cover = new(); cover.BeginInit(); cover.CacheOption = BitmapCacheOption.OnLoad;
                        cover.DecodePixelWidth = 240; cover.StreamSource = memory; cover.EndInit(); cover.Freeze();
                    }
                }
                catch { /* Missing/broken artwork must not prevent playback controls. */ }
            }
            if (version != Volatile.Read(ref _revision)) return;
            _playing = info.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            _playbackRate = info.PlaybackRate is { } rate && double.IsFinite(rate) && rate > 0 ? rate : 1;
            _duration = timeline.EndTime > timeline.StartTime ? timeline.EndTime - timeline.StartTime : TimeSpan.Zero;
            _sampleAt = DateTimeOffset.UtcNow;
            _position = timeline.Position - timeline.StartTime;
            var age = _sampleAt - timeline.LastUpdatedTime;
            if (_playing && age >= TimeSpan.Zero && age < TimeSpan.FromDays(1)) _position += TimeSpan.FromSeconds(age.TotalSeconds * _playbackRate);
            var c = info.Controls;
            Current = new(session.SourceAppUserModelId, props.Title, props.Artist, cover, _position, _duration,
                _playing, c.IsPlayPauseToggleEnabled, c.IsPreviousEnabled, c.IsNextEnabled, c.IsPlaybackPositionEnabled && _duration > TimeSpan.Zero);
            Status = ""; Changed?.Invoke();
        }
        catch { if (version == Volatile.Read(ref _revision)) { Current = null; _playing = false; Status = "Sesi media belum dapat dibaca."; Changed?.Invoke(); } }
    }
    public async Task CommandAsync(string command, double seconds = 0)
    {
        try
        {
            var s = _session; var c = Current;
            if (s is null || c is null) return;
            bool ok = command switch
            {
                "play" when c.CanPlayPause => await s.TryTogglePlayPauseAsync(),
                "previous" when c.CanPrevious => await s.TrySkipPreviousAsync(),
                "next" when c.CanNext => await s.TrySkipNextAsync(),
                "seek" when c.CanSeek => await s.TryChangePlaybackPositionAsync(s.GetTimelineProperties().StartTime.Ticks + TimeSpan.FromSeconds(Math.Clamp(seconds, 0, c.Duration.TotalSeconds)).Ticks),
                _ => true
            };
            if (!ok) { Status = "Pemutar menolak perintah ini."; Changed?.Invoke(); }
        }
        catch { Status = "Pemutar tidak merespons. Pilih sesi media kembali."; Changed?.Invoke(); }
    }
    public void Dispose()
    {
        _disposed = true;
        if (_manager is not null) { _manager.CurrentSessionChanged -= ManagerChanged; _manager.SessionsChanged -= SessionsChanged; }
        if (_session is not null)
        {
            _session.MediaPropertiesChanged -= PropertiesChanged; _session.PlaybackInfoChanged -= PlaybackChanged;
            _session.TimelinePropertiesChanged -= TimelineChanged;
        }
        Interlocked.Increment(ref _revision);
    }
}
