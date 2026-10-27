using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace Maskot.Windows.Platform;

internal sealed record LyricsResult(IReadOnlyList<LyricLine> Lines, string Plain, string Status);
internal sealed class LyricsService : IDisposable
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private CancellationTokenSource? _request;
    private readonly Dictionary<string, LyricsResult> _cache = new();
    public LyricsService() => _http.DefaultRequestHeaders.UserAgent.ParseAdd("Maskot/0.1 (+https://github.com/geminiaiaku-glitch/maskot-project)");
    public async Task<LyricsResult> FetchAsync(MediaView media)
    {
        _request?.Cancel(); _request?.Dispose(); _request = new();
        var token = _request.Token;
        var key = media.Title + "\n" + media.Artist + "\n" + (int)media.Duration.TotalSeconds;
        if (_cache.TryGetValue(key, out var cached)) return cached;
        try
        {
            var url = "https://lrclib.net/api/get?track_name=" + Uri.EscapeDataString(media.Title)
                      + "&artist_name=" + Uri.EscapeDataString(media.Artist)
                      + "&duration=" + Math.Round(media.Duration.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
            using var response = await _http.GetAsync(url, token);
            if (response.StatusCode == HttpStatusCode.NotFound) return new([], "", "Lirik belum tersedia untuk lagu ini.");
            response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
            var root = json.RootElement;
            string Text(string name) => root.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() ?? "" : "";
            var lines = LrcParser.Parse(Text("syncedLyrics"));
            var plain = Text("plainLyrics");
            var result = new LyricsResult(lines, plain, lines.Count > 0 ? "Lirik tersinkron · LRCLIB" : plain.Length > 0 ? "Lirik tanpa sinkronisasi · LRCLIB" : "Lagu instrumental / lirik tidak tersedia.");
            if (_cache.Count >= 32) _cache.Clear();
            _cache[key] = result;
            return result;
        }
        catch (OperationCanceledException) { return new([], "", token.IsCancellationRequested ? "" : "Koneksi lirik melewati batas waktu. Musik tetap dapat dikontrol."); }
        catch { return new([], "", "Lirik tidak dapat dimuat. Periksa koneksi; kontrol musik tetap tersedia."); }
    }
    public void Cancel() => _request?.Cancel();
    public void Dispose() { Cancel(); _request?.Dispose(); _http.Dispose(); }
}
