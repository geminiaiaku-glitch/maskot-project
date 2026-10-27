namespace Maskot.Core;

public enum LyricsAvailability { None, Disabled, Loading, Synced, Plain, Missing, Instrumental, Error }
public enum KaraokeMood { None, Listening, Singing, Confused }
public sealed record SongMetadata(string Source, string Title, string Artist, double DurationSeconds)
{
    public string Identity => Source + "\n" + Title.Trim() + "\n" + Artist.Trim();
    public string LookupKey => Title.Trim().ToLowerInvariant() + "\n" + Artist.Trim().ToLowerInvariant() + "\n" + Math.Round(DurationSeconds);
}
public sealed record LyricsDocument(LyricsAvailability Availability, IReadOnlyList<LyricLine> Lines, string Plain, string Detail)
{
    public static LyricsDocument Empty(LyricsAvailability availability) => new(availability, [], "", "");
    public static LyricsDocument FromText(string? synced, string? plain, bool instrumental = false)
    {
        var lines = LrcParser.Parse(synced ?? "");
        return lines.Any(l => !string.IsNullOrWhiteSpace(l.Text)) ? new(LyricsAvailability.Synced, lines, plain ?? "", "Lirik tersinkron · LRCLIB") :
            !string.IsNullOrWhiteSpace(plain) ? new(LyricsAvailability.Plain, [], plain, "Lirik tanpa sinkronisasi · LRCLIB") :
            Empty(instrumental ? LyricsAvailability.Instrumental : LyricsAvailability.Missing);
    }
}
public interface ILyricsSource
{
    Task<LyricsDocument> FetchAsync(SongMetadata song, CancellationToken cancellationToken, bool force = false);
}

/// <summary>Owns one consent-gated lookup independently of any feature window.</summary>
public sealed class KaraokeSession(ILyricsSource source) : IDisposable
{
    private CancellationTokenSource? _request;
    private SongMetadata? _song;
    private bool _enabled;
    private int _revision;
    private bool _disposed;
    public LyricsDocument Document { get; private set; } = LyricsDocument.Empty(LyricsAvailability.None);
    public Task Pending { get; private set; } = Task.CompletedTask;
    public event Action? Changed;

    public void Update(SongMetadata? song, bool enabled, bool force = false)
    {
        if (_disposed) return;
        if (song is not null && string.IsNullOrWhiteSpace(song.Title)) song = null;
        var durationArrived = _song is { DurationSeconds: <= 0 } && song is { DurationSeconds: > 0 };
        if (!force && song?.Identity == _song?.Identity && enabled == _enabled && !durationArrived) return;
        _song = song; _enabled = enabled;
        var revision = ++_revision;
        _request?.Cancel(); _request?.Dispose(); _request = null;
        if (song is null || !enabled)
        {
            Document = LyricsDocument.Empty(song is null ? LyricsAvailability.None : LyricsAvailability.Disabled);
            Pending = Task.CompletedTask; Changed?.Invoke(); return;
        }
        Document = LyricsDocument.Empty(LyricsAvailability.Loading); Changed?.Invoke();
        _request = new();
        Pending = LoadAsync(song, revision, _request.Token, force);
    }
    private async Task LoadAsync(SongMetadata song, int revision, CancellationToken token, bool force)
    {
        LyricsDocument document;
        try { document = await source.FetchAsync(song, token, force); }
        catch (OperationCanceledException) { return; }
        catch { document = LyricsDocument.Empty(LyricsAvailability.Error); }
        if (_disposed || token.IsCancellationRequested || revision != _revision) return;
        Document = document; Changed?.Invoke();
    }
    public void Dispose()
    {
        _disposed = true; _revision++; _request?.Cancel(); _request?.Dispose();
    }
}

public sealed record KaraokeCue(KaraokeMood Mood, string Current, string Next, string Caption, bool ExpandedNotch, bool ShowBubble, bool Paused, bool Synchronized = false)
{
    public static KaraokeCue Create(LyricsDocument document, bool hasSong, bool playing, TimeSpan position, bool enabled)
    {
        if (!hasSong || !enabled) return new(hasSong && playing ? KaraokeMood.Listening : KaraokeMood.None,
            "", "", hasSong ? "Aktifkan lirik online di Setelan." : "Putar lagu untuk melihat lirik.", false, false, hasSong && !playing);
        var current = ""; var next = ""; var caption = document.Detail; var mood = KaraokeMood.Listening;
        switch (document.Availability)
        {
            case LyricsAvailability.Synced:
                var index = -1;
                for (var i = 0; i < document.Lines.Count && document.Lines[i].At <= position; i++) index = i;
                current = index >= 0 ? document.Lines[index].Text : "";
                next = document.Lines.Skip(index + 1).FirstOrDefault(l => !string.IsNullOrWhiteSpace(l.Text))?.Text ?? "";
                caption = "Lirik tersinkron · LRCLIB";
                mood = string.IsNullOrWhiteSpace(current) ? KaraokeMood.Listening : KaraokeMood.Singing;
                if (string.IsNullOrWhiteSpace(current)) current = "♪";
                break;
            case LyricsAvailability.Plain:
                current = document.Plain.Split('\n').FirstOrDefault(l => !string.IsNullOrWhiteSpace(l))?.Trim() ?? "♪";
                next = "Lirik tanpa sinkronisasi"; caption = "Lirik tersedia tanpa penanda waktu · LRCLIB"; mood = KaraokeMood.Singing;
                break;
            case LyricsAvailability.Missing:
                caption = "Lirik belum tersedia · LRCLIB"; mood = KaraokeMood.Confused; break;
            case LyricsAvailability.Instrumental:
                caption = "Lagu instrumental"; mood = KaraokeMood.Confused; break;
            case LyricsAvailability.Error:
                caption = "Koneksi lirik bermasalah"; mood = KaraokeMood.Confused; break;
            default:
                caption = "Mencari di LRCLIB"; break;
        }
        if (!playing) { mood = KaraokeMood.None; caption = "Dijeda · " + caption; }
        return new(mood, current, next, caption,
            document.Availability is LyricsAvailability.Synced or LyricsAvailability.Plain,
            mood == KaraokeMood.Singing, !playing, document.Availability == LyricsAvailability.Synced);
    }
}

public static class PlaybackPosition
{
    public static TimeSpan Estimate(TimeSpan sample, TimeSpan duration, TimeSpan elapsed, bool playing, double rate = 1)
    {
        if (!double.IsFinite(rate) || rate <= 0) rate = 1;
        var seconds = Math.Max(0, sample.TotalSeconds + (playing ? Math.Max(0, elapsed.TotalSeconds) * rate : 0));
        return TimeSpan.FromSeconds(duration > TimeSpan.Zero ? Math.Min(seconds, duration.TotalSeconds) : seconds);
    }
}
