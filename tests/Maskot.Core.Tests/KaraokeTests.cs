using Maskot.Core;

namespace Maskot.Core.Tests;

public sealed class KaraokeTests
{
    private static readonly SongMetadata Song = new("player", "Lagu uji Momo", "Momo Studio", 60);
    private static readonly LyricsDocument Synced = LyricsDocument.FromText("[00:00.00]Langkah kecil menemani hari\n[00:03.00]Nada ringan membawa cerita\n[00:07.00]\n[00:09.00]Momo tersenyum di sini", null);
    [Fact] public async Task ConsentIsRequiredBeforeAnyLookup()
    {
        var source = new Source(); using var session = new KaraokeSession(source);
        session.Update(Song, false); await session.Pending;
        Assert.Equal(0, source.Calls); Assert.Equal(LyricsAvailability.Disabled, session.Document.Availability);
        session.Update(Song, true); await session.Pending; Assert.Equal(1, source.Calls);
    }
    [Fact] public async Task MetadataEventsDoNotFetchSameSongRepeatedly()
    {
        var source = new Source(); using var session = new KaraokeSession(source);
        session.Update(Song, true); await session.Pending;
        for (var i = 0; i < 100; i++) session.Update(Song with { DurationSeconds = 60 + i * .01 }, true);
        Assert.Equal(1, source.Calls);
    }
    [Fact] public async Task DurationArrivingCausesOnlyOneAdditionalLookup()
    {
        var source = new Source(); using var session = new KaraokeSession(source);
        session.Update(Song with { DurationSeconds = 0 }, true); await session.Pending;
        session.Update(Song, true); await session.Pending; session.Update(Song with { DurationSeconds = 61 }, true);
        Assert.Equal(2, source.Calls);
    }
    [Fact] public async Task OldSongResponseCannotReplaceNewSongLyrics()
    {
        var old = new TaskCompletionSource<LyricsDocument>(TaskCreationOptions.RunContinuationsAsynchronously);
        var source = new Source { Handler = (s, _) => s.Title == Song.Title ? old.Task : Task.FromResult(LyricsDocument.FromText("[00:00]Lagu baru", null)) };
        using var session = new KaraokeSession(source);
        session.Update(Song, true); var oldTask = session.Pending;
        session.Update(Song with { Title = "new" }, true); await session.Pending;
        old.SetResult(Synced); await oldTask;
        Assert.Equal("Lagu baru", session.Document.Lines[0].Text);
    }
    [Fact] public async Task DisablingLyricsCancelsAndRejectsPendingResult()
    {
        var pending = new TaskCompletionSource<LyricsDocument>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var session = new KaraokeSession(new Source { Handler = (_, _) => pending.Task });
        session.Update(Song, true); var task = session.Pending; session.Update(Song, false);
        pending.SetResult(Synced); await task;
        Assert.Equal(LyricsAvailability.Disabled, session.Document.Availability);
    }
    [Fact] public async Task NoMediaClearsPreviousLyrics()
    {
        using var session = new KaraokeSession(new Source()); session.Update(Song, true); await session.Pending;
        session.Update(null, true); Assert.Equal(LyricsAvailability.None, session.Document.Availability);
    }
    [Fact] public async Task ForceRefreshRetriesFailedLookup()
    {
        var source = new Source(); using var session = new KaraokeSession(source);
        session.Update(Song, true); await session.Pending; session.Update(Song, true, true); await session.Pending;
        Assert.Equal(2, source.Calls);
    }
    [Fact] public async Task ProviderFailureBecomesErrorWithoutThrowingToUi()
    {
        using var session = new KaraokeSession(new Source { Handler = (_, _) => throw new InvalidOperationException("offline") });
        session.Update(Song, true); await session.Pending; Assert.Equal(LyricsAvailability.Error, session.Document.Availability);
    }
    [Fact] public void LyricsFollowSeekForwardAndBackward()
    {
        Assert.Equal("Nada ringan membawa cerita", Cue(5).Current);
        Assert.Equal("Langkah kecil menemani hari", Cue(1).Current);
        Assert.Equal("Momo tersenyum di sini", Cue(10).Current);
    }
    [Fact] public void TimedGapsDoNotHighlightFutureLyrics()
    {
        var cue = Cue(8); Assert.Equal("♪", cue.Current); Assert.Equal(KaraokeMood.Listening, cue.Mood);
        Assert.Equal("Momo tersenyum di sini", cue.Next);
    }
    [Fact] public void PausingStopsSingingAndHidesSpeechButKeepsNotchLyrics()
    {
        var cue = KaraokeCue.Create(Synced, true, false, TimeSpan.FromSeconds(5), true);
        Assert.Equal(KaraokeMood.None, cue.Mood); Assert.False(cue.ShowBubble); Assert.True(cue.ExpandedNotch);
        Assert.Equal("Nada ringan membawa cerita", cue.Current); Assert.True(cue.Paused);
    }
    [Theory] [InlineData(LyricsAvailability.Missing)] [InlineData(LyricsAvailability.Instrumental)] [InlineData(LyricsAvailability.Error)]
    public void UnavailableLyricsMakeMomoConfused(LyricsAvailability state)
    {
        var cue = KaraokeCue.Create(LyricsDocument.Empty(state), true, true, TimeSpan.Zero, true);
        Assert.Equal(KaraokeMood.Confused, cue.Mood); Assert.False(cue.ShowBubble); Assert.False(cue.ExpandedNotch);
        Assert.Empty(cue.Current); Assert.Empty(cue.Next);
    }
    [Fact] public void LoadingUsesPatientPoseInsteadOfClaimingMissingLyrics()
    {
        var cue = KaraokeCue.Create(LyricsDocument.Empty(LyricsAvailability.Loading), true, true, TimeSpan.Zero, true);
        Assert.Equal(KaraokeMood.Listening, cue.Mood); Assert.Empty(cue.Current); Assert.False(cue.ShowBubble);
        Assert.False(cue.ExpandedNotch);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void NoSongHasNoMicrophoneOrBubble(bool enabled)
    {
        var cue = KaraokeCue.Create(Synced, false, true, TimeSpan.Zero, enabled);
        Assert.Equal(KaraokeMood.None, cue.Mood); Assert.False(cue.ShowBubble); Assert.False(cue.ExpandedNotch);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void MicrophoneFollowsPlaybackEvenWhenOnlineLyricsAreOff(bool playing)
    {
        var cue = KaraokeCue.Create(LyricsDocument.Empty(LyricsAvailability.Disabled), true, playing, TimeSpan.Zero, false);
        Assert.Equal(playing ? KaraokeMood.Listening : KaraokeMood.None, cue.Mood);
        Assert.False(cue.ShowBubble); Assert.False(cue.ExpandedNotch); Assert.Empty(cue.Current);
    }
    [Theory]
    [InlineData(LyricsAvailability.Synced)] [InlineData(LyricsAvailability.Plain)]
    [InlineData(LyricsAvailability.Missing)] [InlineData(LyricsAvailability.Instrumental)]
    [InlineData(LyricsAvailability.Error)] [InlineData(LyricsAvailability.Loading)]
    public void PauseAndResumeRestoreTheAppropriatePose(LyricsAvailability state)
    {
        var doc = state == LyricsAvailability.Synced ? Synced : state == LyricsAvailability.Plain
            ? LyricsDocument.FromText(null, "Lirik uji Momo") : LyricsDocument.Empty(state);
        var playing = KaraokeCue.Create(doc, true, true, TimeSpan.FromSeconds(5), true);
        var paused = KaraokeCue.Create(doc, true, false, TimeSpan.FromSeconds(5), true);
        var resumed = KaraokeCue.Create(doc, true, true, TimeSpan.FromSeconds(5), true);
        Assert.Equal(KaraokeMood.None, paused.Mood); Assert.True(paused.Paused); Assert.False(paused.ShowBubble);
        Assert.Equal(playing, resumed); Assert.NotEqual(KaraokeMood.None, resumed.Mood);
    }
    [Fact] public void PlainLyricsAreMarkedUntimedAndNeverInventLineTimings()
    {
        var doc = LyricsDocument.FromText(null, "Lirik contoh satu\nLirik contoh dua");
        var a = KaraokeCue.Create(doc, true, true, TimeSpan.Zero, true);
        var b = KaraokeCue.Create(doc, true, true, TimeSpan.FromSeconds(50), true);
        Assert.Equal(a.Current, b.Current); Assert.Contains("tanpa sinkronisasi", a.Next);
    }
    [Fact] public void SingingAddsMouthMotionWhileKeepingWalkingFeet()
    {
        var a = MascotAnimation.Sample(MascotPose.Walk, .18, true, KaraokeMood.Singing);
        var b = MascotAnimation.Sample(MascotPose.Walk, .4, true, KaraokeMood.Singing);
        Assert.True(a.MouthOpen > 0); Assert.NotEqual(a.MouthOpen, b.MouthOpen);
        Assert.True(a.RightArm < 0); Assert.True(a.LeftFoot > 20); Assert.True(a.RightFoot < -20);
    }
    [Fact] public void ConfusedPoseTiltsHeadAndShrugsWhileInspectingMicrophone()
    {
        var f = MascotAnimation.Sample(MascotPose.Idle, .3, true, KaraokeMood.Confused);
        var later = MascotAnimation.Sample(MascotPose.Idle, .8, true, KaraokeMood.Confused);
        Assert.True(f.HeadAngle > 8); Assert.True(f.LeftArm < 0); Assert.Equal(0, f.MouthOpen);
        Assert.NotEqual(f.HeadAngle, later.HeadAngle); Assert.NotEqual(f.RightArm, later.RightArm);
    }
    [Fact] public void DraggingHasPriorityOverKaraokePose()
    {
        Assert.Equal(MascotAnimation.Sample(MascotPose.Lifted, .2), MascotAnimation.Sample(MascotPose.Lifted, .2, true, KaraokeMood.Singing));
    }
    [Fact] public void ReducedMotionStillHoldsMicrophoneWithoutAnimatedMouth()
    {
        var a = MascotAnimation.Sample(MascotPose.Idle, .2, false, KaraokeMood.Singing);
        var b = MascotAnimation.Sample(MascotPose.Idle, 3, false, KaraokeMood.Singing);
        Assert.Equal(a, b); Assert.True(a.RightArm < 0); Assert.Equal(0, a.MouthOpen);
    }
    [Fact] public void UnknownDurationStillAdvancesLyricsTimeline()
    {
        Assert.Equal(TimeSpan.FromSeconds(13), PlaybackPosition.Estimate(TimeSpan.FromSeconds(10), TimeSpan.Zero, TimeSpan.FromSeconds(3), true));
    }
    [Fact] public void PlaybackRatePauseAndDurationAreRespected()
    {
        Assert.Equal(TimeSpan.FromSeconds(16), PlaybackPosition.Estimate(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(3), true, 2));
        Assert.Equal(TimeSpan.FromSeconds(10), PlaybackPosition.Estimate(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(3), false));
        Assert.Equal(TimeSpan.FromSeconds(60), PlaybackPosition.Estimate(TimeSpan.FromSeconds(59), TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(3), true));
    }
    [Fact] public void ExpandingNotchKeepsItCenteredAndDefersResizeDuringDrag()
    {
        var d = new DisplayArea("d", new(-1920, 0, 1920, 1080), new(-1920, 0, 1920, 1040), 1, 1035);
        var c = new MascotController(); c.Restore(new(MascotMode.Notch, new(-1000, 0), "d"), [d]);
        c.SetLyricNotch(true, [d]); Assert.Equal((-1170d, 0d), (c.Position.X, c.Position.Y)); Assert.Equal((420d, 96d), c.Dimensions(1));
        c.Press(c.Position + new PointD(20, 10)); c.SetLyricNotch(false, [d]); Assert.Equal(420, c.NotchWidth);
        c.CancelDrag(); c.SetLyricNotch(false, [d]); Assert.Equal(230, c.NotchWidth);
    }
    [Fact] public void LyricBubbleIsClampedAndFlipsBelowTopEdge()
    {
        var work = new RectD(-1920, 0, 1920, 1040);
        var p = DesktopGeometry.PlaceLyrics(new(-1900, 3, 112, 142), work, 308, 106);
        Assert.Equal(-1920, p.X); Assert.True(p.Y >= 145); Assert.True(p.X + 308 <= work.Right);
    }
    [Fact] public void DuplicateTimestampsAndBlankGapsKeepExistingSelectionSemantics()
    {
        var doc = LyricsDocument.FromText("[00:01]first\n[00:01]last\n[00:02]\n[00:03]next", null);
        var before = KaraokeCue.Create(doc, true, true, TimeSpan.Zero, true);
        Assert.Equal("♪", before.Current); Assert.Equal("first", before.Next);
        var exact = KaraokeCue.Create(doc, true, true, TimeSpan.FromSeconds(1), true);
        Assert.Equal("last", exact.Current); Assert.Equal("next", exact.Next);
        Assert.Equal("last", LrcParser.Current(doc.Lines, TimeSpan.FromSeconds(1)));
        var gap = KaraokeCue.Create(doc, true, true, TimeSpan.FromSeconds(2), true);
        Assert.Equal("♪", gap.Current); Assert.False(gap.ShowBubble); Assert.Equal("next", gap.Next);
    }
    [Fact] public void PlainLyricsSkipWhitespaceAndTrimCrLf()
    {
        var doc = LyricsDocument.FromText(null, "  \r\n\t\r\n  first line  \r\nsecond line");
        Assert.Equal("first line", KaraokeCue.Create(doc, true, true, TimeSpan.Zero, true).Current);
    }
    [Fact] public void ExtremePlaybackRateCannotOverflowTimeline()
    {
        Assert.Equal(TimeSpan.MaxValue, PlaybackPosition.Estimate(TimeSpan.FromSeconds(1), TimeSpan.Zero, TimeSpan.FromSeconds(10), true, double.MaxValue));
        Assert.Equal(TimeSpan.FromSeconds(60), PlaybackPosition.Estimate(TimeSpan.Zero, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(10), true, double.MaxValue));
    }
    [Fact] public async Task DisposingCancelsPendingLookupAndRejectsLateCompletion()
    {
        var result = new TaskCompletionSource<LyricsDocument>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken token = default;
        var source = new Source { Handler = (_, cancellation) => { token = cancellation; return result.Task; } };
        var session = new KaraokeSession(source); session.Update(Song, true); var pending = session.Pending;
        var changes = 0; session.Changed += () => changes++;
        session.Dispose(); Assert.True(token.IsCancellationRequested);
        result.SetResult(Synced); await pending;
        session.Update(Song, true, true);
        Assert.Equal(0, changes); Assert.Equal(1, source.Calls);
    }
    private static KaraokeCue Cue(double seconds) => KaraokeCue.Create(Synced, true, true, TimeSpan.FromSeconds(seconds), true);
    private sealed class Source : ILyricsSource
    {
        public int Calls;
        public Func<SongMetadata, CancellationToken, Task<LyricsDocument>> Handler = (_, _) => Task.FromResult(Synced);
        public Task<LyricsDocument> FetchAsync(SongMetadata song, CancellationToken token, bool force = false) { Calls++; return Handler(song, token); }
    }
}
