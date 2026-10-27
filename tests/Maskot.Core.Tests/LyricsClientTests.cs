using System.Net;
using System.Net.Http;
using System.Text.Json;
using Maskot.Core;

namespace Maskot.Core.Tests;

public sealed class LyricsClientTests
{
    private static readonly SongMetadata Song = new("p", "Lagu Uji", "Momo Studio", 60);
    private static HttpResponseMessage Json(object value, HttpStatusCode status = HttpStatusCode.OK) => new(status) { Content = new StringContent(JsonSerializer.Serialize(value)) };
    [Fact] public async Task SyncedLyricsAreParsedAndCachedUntilForceRefresh()
    {
        var handler = new Handler(_ => Json(new { syncedLyrics = "[00:01]Contoh buatan Momo", plainLyrics = "", instrumental = false }));
        using var http = new HttpClient(handler); using var client = new LyricsClient(http);
        var doc = await client.FetchAsync(Song, default); await client.FetchAsync(Song, default);
        Assert.Equal(LyricsAvailability.Synced, doc.Availability); Assert.Equal("Contoh buatan Momo", doc.Lines.Single().Text); Assert.Single(handler.Requests);
        await client.FetchAsync(Song, default, true); Assert.Equal(2, handler.Requests.Count);
    }
    [Fact] public async Task NotFoundSearchDoesNotChooseUnrelatedLyrics()
    {
        var handler = new Handler(u => u.AbsolutePath.EndsWith("/get") ? Json(new { }, HttpStatusCode.NotFound) : Json(new[] { new { trackName = "Lagu lain", artistName = "Momo Studio", duration = 60, syncedLyrics = "[00:00]Wrong" } }));
        using var http = new HttpClient(handler); using var client = new LyricsClient(http);
        Assert.Equal(LyricsAvailability.Missing, (await client.FetchAsync(Song, default)).Availability); Assert.Equal(2, handler.Requests.Count);
    }
    [Fact] public async Task SearchAcceptsMatchingSignatureAndRejectsWrongVersionDuration()
    {
        var handler = new Handler(u => u.AbsolutePath.EndsWith("/get") ? Json(new { }, HttpStatusCode.NotFound) : Json(new[] {
            new { trackName = "Lagu Uji", artistName = "Momo Studio", duration = 120, syncedLyrics = "[00:00]Wrong version" },
            new { trackName = "Lagu Uji", artistName = "Momo Studio", duration = 61, syncedLyrics = "[00:00]Contoh tepat" }}));
        using var http = new HttpClient(handler); using var client = new LyricsClient(http);
        Assert.Equal("Contoh tepat", (await client.FetchAsync(Song, default)).Lines.Single().Text);
    }
    [Fact] public async Task UnknownDurationIsOmittedInsteadOfSendingZero()
    {
        var handler = new Handler(_ => Json(new { instrumental = true }));
        using var http = new HttpClient(handler); using var client = new LyricsClient(http);
        Assert.Equal(LyricsAvailability.Instrumental, (await client.FetchAsync(Song with { DurationSeconds = 0 }, default)).Availability);
        Assert.DoesNotContain("duration=", handler.Requests.Single().Query);
    }
    [Fact] public async Task PlainLyricsAreNotFalselyTreatedAsSynced()
    {
        var handler = new Handler(_ => Json(new { plainLyrics = "Contoh tanpa waktu", syncedLyrics = (string?)null }));
        using var http = new HttpClient(handler); using var client = new LyricsClient(http);
        Assert.Equal(LyricsAvailability.Plain, (await client.FetchAsync(Song, default)).Availability);
    }
    [Fact] public async Task ConnectionFailureDoesNotThrowToPlayback()
    {
        var handler = new Handler(_ => throw new HttpRequestException("offline"));
        using var http = new HttpClient(handler); using var client = new LyricsClient(http);
        Assert.Equal(LyricsAvailability.Error, (await client.FetchAsync(Song, default)).Availability);
    }
    [Fact] public async Task CancellationRemainsCancellation()
    {
        using var http = new HttpClient(new Handler(_ => Json(new { }))); using var client = new LyricsClient(http);
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.FetchAsync(Song, cancel.Token));
    }
    [Fact] public async Task SearchWithoutArtistRejectsAmbiguousSongs()
    {
        var handler = new Handler(_ => Json(new[] {
            new { trackName = "Lagu Uji", artistName = "A", duration = 60, plainLyrics = "a" },
            new { trackName = "Lagu Uji", artistName = "B", duration = 60, plainLyrics = "b" }}));
        using var http = new HttpClient(handler); using var client = new LyricsClient(http);
        Assert.Equal(LyricsAvailability.Missing, (await client.FetchAsync(Song with { Artist = "" }, default)).Availability);
        Assert.EndsWith("/search", handler.Requests.Single().AbsolutePath);
    }
    [Fact] public void NormalizationRemovesPromoButPreservesLiveAndRemixVersions()
    {
        Assert.Equal("lagu uji", LyricsClient.NormalizeTitle("Lagu Uji (Official Music Video)"));
        Assert.Equal("lagu uji live", LyricsClient.NormalizeTitle("Lagu Uji (Live)"));
        Assert.Equal("lagu uji remix", LyricsClient.NormalizeTitle("Lagu Uji [Remix]"));
    }
    private sealed class Handler(Func<Uri, HttpResponseMessage> response) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Requests.Add(request.RequestUri!); return Task.FromResult(response(request.RequestUri!)); }
    }
}
