using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Maskot.Core;

public sealed partial class LyricsClient : ILyricsSource, IDisposable
{
    private readonly HttpClient _http;
    private readonly bool _ownsClient;
    private readonly ConcurrentDictionary<string, (DateTimeOffset At, LyricsDocument Doc)> _cache = new();
    public LyricsClient(HttpClient? client = null)
    {
        _ownsClient = client is null;
        _http = client ?? new() { Timeout = TimeSpan.FromSeconds(10), MaxResponseContentBufferSize = 1024 * 1024 };
        if (_http.DefaultRequestHeaders.UserAgent.Count == 0)
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("Maskot/0.3 (+https://github.com/geminiaiaku-glitch/maskot-project)");
    }
    [GeneratedRegex(@"[\(\[]\s*(?:(?:official\s+)?(?:music\s+)?(?:video|audio|lyrics?|lyric\s+video)|hd|4k)\s*[\)\]]", RegexOptions.IgnoreCase)]
    private static partial Regex Promotion();
    [GeneratedRegex(@"[^\p{L}\p{N}]+")]
    private static partial Regex Punctuation();
    public static string NormalizeTitle(string title) => Punctuation().Replace(Promotion().Replace(title, "").ToLowerInvariant(), " ").Trim();
    private static string NormalizeArtist(string artist) => Punctuation().Replace(artist.ToLowerInvariant(), " ").Trim();

    public async Task<LyricsDocument> FetchAsync(SongMetadata song, CancellationToken cancellationToken, bool force = false)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!force && _cache.TryGetValue(song.LookupKey, out var cached) && DateTimeOffset.UtcNow - cached.At < TimeSpan.FromMinutes(20)) return cached.Doc;
        try
        {
            var query = "track_name=" + Uri.EscapeDataString(Promotion().Replace(song.Title, "").Trim())
                        + (string.IsNullOrWhiteSpace(song.Artist) ? "" : "&artist_name=" + Uri.EscapeDataString(song.Artist.Trim()));
            var duration = song.DurationSeconds > 0 && double.IsFinite(song.DurationSeconds) ? "&duration=" + Math.Round(song.DurationSeconds).ToString(CultureInfo.InvariantCulture) : "";
            using var response = string.IsNullOrWhiteSpace(song.Artist) ? null : await _http.GetAsync("https://lrclib.net/api/get?" + query + duration, cancellationToken);
            LyricsDocument doc;
            if (response is null || response.StatusCode == HttpStatusCode.NotFound)
            {
                using var search = await _http.GetAsync("https://lrclib.net/api/search?" + query, cancellationToken);
                search.EnsureSuccessStatusCode();
                using var results = JsonDocument.Parse(await search.Content.ReadAsStringAsync(cancellationToken));
                var candidates = results.RootElement.ValueKind == JsonValueKind.Array ? results.RootElement.EnumerateArray().Take(100).ToArray() : [];
                var chosen = candidates.Where(r => NormalizeTitle(Text(r, "trackName")) == NormalizeTitle(song.Title)
                    && (string.IsNullOrWhiteSpace(song.Artist) || NormalizeArtist(Text(r, "artistName")) == NormalizeArtist(song.Artist))
                    && (song.DurationSeconds <= 0 || Math.Abs(Duration(r) - song.DurationSeconds) <= 8))
                    .OrderBy(r => song.DurationSeconds > 0 ? Math.Abs(Duration(r) - song.DurationSeconds) : 0)
                    .ThenByDescending(r => !string.IsNullOrWhiteSpace(Text(r, "syncedLyrics"))).ToArray();
                var ambiguousArtist = string.IsNullOrWhiteSpace(song.Artist) && chosen.Select(r => NormalizeArtist(Text(r, "artistName"))).Distinct().Count() > 1;
                doc = chosen.Length == 0 || ambiguousArtist ? LyricsDocument.Empty(LyricsAvailability.Missing) : Read(chosen[0]);
            }
            else
            {
                response.EnsureSuccessStatusCode();
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                doc = Read(json.RootElement);
            }
            if (_cache.Count >= 32) _cache.Clear();
            _cache[song.LookupKey] = (DateTimeOffset.UtcNow, doc);
            return doc;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return LyricsDocument.Empty(LyricsAvailability.Error); }
    }
    private static string Text(JsonElement json, string name) => json.ValueKind == JsonValueKind.Object && json.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
    private static double Duration(JsonElement json) => json.ValueKind == JsonValueKind.Object && json.TryGetProperty("duration", out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var duration) ? duration : 0;
    private static LyricsDocument Read(JsonElement json) => LyricsDocument.FromText(Text(json, "syncedLyrics"), Text(json, "plainLyrics"),
        json.TryGetProperty("instrumental", out var value) && value.ValueKind == JsonValueKind.True);
    public void Dispose() { if (_ownsClient) _http.Dispose(); }
}
