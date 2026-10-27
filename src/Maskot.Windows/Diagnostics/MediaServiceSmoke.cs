using Maskot.Windows.Platform;

namespace Maskot.Windows.Diagnostics;

internal static class MediaServiceSmoke
{
    public static async Task RunAsync(Action<string, bool, object?> check)
    {
        using var service = new MediaService();
        var first = new Session("first");
        service.SetSession(first);
        check("media_initial_metadata_loaded_once", first.Reads == 1 && service.Current?.Title == "first", first.Reads);
        first.Playing = false;
        for (var i = 0; i < 30; i++) first.Playback();
        check("media_timeline_events_reuse_artwork", first.Reads == 1 && service.Current?.Playing == false, first.Reads);

        // Match WinRT callbacks arriving off the UI thread.
        first.Playing = true;
        await Task.Run(first.Playback);
        await Task.Delay(30);
        check("media_background_callback_returns_to_dispatcher", service.Current?.Playing == true && first.Reads == 1, null);

        var gate = new TaskCompletionSource<MediaMetadata>(TaskCreationOptions.RunContinuationsAsynchronously);
        first.Responses.Enqueue(gate.Task);
        var pending = service.RefreshAsync();
        for (var i = 0; i < 30; i++) first.Metadata();
        first.Playing = false; first.Playback();
        check("media_pause_does_not_wait_for_artwork", service.Current?.Playing == false && !pending.IsCompleted, null);
        first.Title = "latest";
        gate.SetResult(new("stale", "fixture", null));
        await pending;
        check("media_metadata_bursts_coalesce_and_keep_latest", first.Reads == 3 && service.Current?.Title == "latest" && service.Current.Playing == false, first.Reads);

        gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        first.Responses.Enqueue(gate.Task);
        pending = service.RefreshAsync();
        var queuedOldPlayback = first.PlaybackCallback;
        var second = new Session("second");
        service.SetSession(second);
        await Task.Run(() => queuedOldPlayback?.Invoke());
        gate.SetResult(new("old session", "fixture", null));
        await pending;
        check("media_session_switch_discards_late_metadata_and_callbacks", first.Disposed && first.LastToken.IsCancellationRequested && service.Current?.Source == "second" && service.Current.Title == "second", null);

        second.Responses.Enqueue(Task.FromException<MediaMetadata>(new IOException("fixture transient failure")));
        await service.RefreshAsync();
        check("media_transient_metadata_failure_is_contained", service.Current is null, null);
        second.Playback();
        check("media_recovers_metadata_after_transient_failure", service.Current?.Title == "second" && second.Reads == 3, second.Reads);
        second.FailPlayback = true; second.Playback();
        check("media_transient_playback_failure_is_contained", service.Current is null, null);
        second.FailPlayback = false; second.Playback();
        check("media_recovers_playback_without_redecoding_artwork", service.Current?.Title == "second" && second.Reads == 3, second.Reads);

        gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        second.Responses.Enqueue(gate.Task);
        pending = service.RefreshAsync();
        var changes = 0; service.Changed += () => changes++;
        service.Dispose();
        gate.SetResult(new("after disposal", "fixture", null));
        await pending;
        check("media_disposal_ignores_pending_result", second.Disposed && second.LastToken.IsCancellationRequested && changes == 0 && service.Current is null, null);
    }

    private sealed class Session(string source) : IMediaSession
    {
        public string Source { get; } = source;
        public string Title { get; set; } = source;
        public bool Playing { get; set; } = true;
        public bool FailPlayback { get; set; }
        public CancellationToken LastToken { get; private set; }
        public int Reads { get; private set; }
        public bool Disposed { get; private set; }
        public Queue<Task<MediaMetadata>> Responses { get; } = new();
        public event Action? MetadataChanged;
        public event Action? PlaybackChanged;
        public Action? PlaybackCallback => PlaybackChanged;
        public void Metadata() => MetadataChanged?.Invoke();
        public void Playback() => PlaybackChanged?.Invoke();
        public MediaPlayback ReadPlayback() => FailPlayback ? throw new IOException("fixture transient playback failure")
            : new(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(100), DateTimeOffset.UtcNow, Playing, 1, true, true, true, true);
        public Task<MediaMetadata> ReadMetadataAsync(CancellationToken cancellationToken)
        {
            LastToken = cancellationToken; Reads++;
            return Responses.Count > 0 ? Responses.Dequeue() : Task.FromResult(new MediaMetadata(Title, "fixture", null));
        }
        public Task<bool> CommandAsync(string command, double seconds) => Task.FromResult(true);
        public void Dispose() { Disposed = true; MetadataChanged = null; PlaybackChanged = null; }
    }
}
