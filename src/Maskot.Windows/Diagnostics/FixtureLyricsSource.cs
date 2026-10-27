namespace Maskot.Windows.Diagnostics;

internal sealed class FixtureLyricsSource : ILyricsSource
{
    public LyricsDocument Document { get; set; } = LyricsDocument.Empty(LyricsAvailability.None);
    public int Requests { get; private set; }
    public Task<LyricsDocument> FetchAsync(SongMetadata song, CancellationToken cancellationToken, bool force = false)
    {
        Requests++; return Task.FromResult(Document);
    }
}
