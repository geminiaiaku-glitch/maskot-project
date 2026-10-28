using Maskot.Core;

namespace Maskot.Core.Tests;

public sealed class NotificationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 9, 0, 0, TimeSpan.Zero);
    private static DesktopNotice Notice(uint id, string source = "test.app", int seconds = 0) =>
        new(id, source, "Aplikasi uji", "Judul buatan", "Pesan buatan untuk pengujian", Now.AddSeconds(seconds));

    [Fact] public void ExistingWindowsNotificationsAreOnlyABaseline()
    {
        var feed = new NotificationFeed();
        Assert.False(feed.ApplySnapshot([Notice(1), Notice(2)], Now));
        Assert.Null(feed.Current); Assert.Empty(feed.Recent); Assert.Equal(0, feed.PendingCount);
        Assert.True(feed.ApplySnapshot([Notice(1), Notice(2), Notice(3)], Now));
        Assert.Equal(3u, feed.Current?.Id); Assert.Single(feed.Recent);
    }
    [Fact] public void DuplicateUpdatesAndReappearingIdsDoNotReplay()
    {
        var feed = new NotificationFeed(); feed.ApplySnapshot([], Now);
        feed.ApplySnapshot([Notice(1), Notice(1)], Now);
        feed.DismissCurrent(Now); feed.ApplySnapshot([], Now);
        Assert.False(feed.ApplySnapshot([Notice(1) with { Body = "Updated text" }], Now));
        Assert.Null(feed.Current); Assert.Single(feed.Recent);
    }
    [Fact] public void ReusedIdFromAnotherAppOrCreationTimeIsANewNotice()
    {
        var feed = new NotificationFeed(); feed.ApplySnapshot([], Now);
        feed.ApplySnapshot([Notice(1), Notice(1, "other.app"), Notice(1, seconds: 1)], Now);
        Assert.Equal(3, feed.Recent.Count); Assert.Equal(2, feed.PendingCount);
    }
    [Fact] public void OwnMomoNotificationsAreIgnored()
    {
        var feed = new NotificationFeed("momo.app"); feed.ApplySnapshot([], Now);
        Assert.False(feed.ApplySnapshot([Notice(1, "MOMO.APP")], Now));
        Assert.Empty(feed.Recent); Assert.Null(feed.Current);
    }
    [Fact] public void BurstsKeepBoundedRecentHistoryAndNewestPendingNotices()
    {
        var feed = new NotificationFeed(); feed.ApplySnapshot([], Now);
        feed.ApplySnapshot(Enumerable.Range(1, 100).Select(i => Notice((uint)i, seconds: i)), Now);
        Assert.Equal(30, feed.Recent.Count); Assert.Equal(100u, feed.Recent[0].Id);
        Assert.Equal(91u, feed.Current?.Id); Assert.Equal(9, feed.PendingCount);
        Assert.Equal(71u, feed.Recent[^1].Id);
    }
    [Fact] public void LargeWindowsBaselineDoesNotReplayAfterIdentityCacheLimit()
    {
        var feed = new NotificationFeed();
        var snapshot = Enumerable.Range(1, 5000).Select(i => Notice((uint)i)).ToArray();
        feed.ApplySnapshot(snapshot, Now);
        Assert.False(feed.ApplySnapshot(snapshot, Now)); Assert.Empty(feed.Recent);
    }
    [Fact] public void CardsAdvanceAfterEightSecondsAndHoldExtendsDisplay()
    {
        var feed = new NotificationFeed(); feed.ApplySnapshot([], Now);
        feed.ApplySnapshot([Notice(1), Notice(2)], Now);
        Assert.False(feed.Advance(Now.AddSeconds(7), false));
        Assert.False(feed.Advance(Now.AddSeconds(8), true));
        Assert.False(feed.Advance(Now.AddSeconds(15), false));
        Assert.True(feed.Advance(Now.AddSeconds(16), false)); Assert.Equal(2u, feed.Current?.Id);
        Assert.True(feed.Advance(Now.AddSeconds(24), false)); Assert.Null(feed.Current);
    }
    [Fact] public void LocalDismissAndClearDoNotReplayWindowsSnapshot()
    {
        var feed = new NotificationFeed(); feed.ApplySnapshot([], Now);
        var snapshot = new[] { Notice(1), Notice(2) }; feed.ApplySnapshot(snapshot, Now);
        Assert.True(feed.DismissCurrent(Now)); Assert.Equal(2u, feed.Current?.Id);
        Assert.Equal(2, feed.Recent.Count); Assert.True(feed.Clear());
        Assert.False(feed.ApplySnapshot(snapshot, Now)); Assert.Empty(feed.Recent); Assert.Null(feed.Current);
    }
    [Fact] public void LockOrQuietDropsCardsAndBaselinesTheResumeGap()
    {
        var feed = new NotificationFeed(); feed.ApplySnapshot([], Now);
        feed.ApplySnapshot([Notice(1), Notice(2)], Now);
        feed.SuspendPresentation(true);
        Assert.Null(feed.Current); Assert.Equal(0, feed.PendingCount);
        feed.ApplySnapshot([Notice(1), Notice(2), Notice(3)], Now);
        Assert.Equal(2, feed.Recent.Count); Assert.False(feed.Advance(Now.AddSeconds(30), false));
        feed.SuspendPresentation(false);
        feed.ApplySnapshot([Notice(1), Notice(2), Notice(3), Notice(4)], Now);
        Assert.Null(feed.Current);
        feed.ApplySnapshot([Notice(1), Notice(2), Notice(3), Notice(4), Notice(5)], Now);
        Assert.Equal(5u, feed.Current?.Id);
    }
    [Fact] public void DisablingPurgesHistoryAndNextEnableStartsWithABaseline()
    {
        var feed = new NotificationFeed(); feed.ApplySnapshot([], Now); feed.ApplySnapshot([Notice(1)], Now);
        feed.Reset(); Assert.Null(feed.Current); Assert.Empty(feed.Recent); Assert.Equal(0, feed.PendingCount);
        Assert.False(feed.ApplySnapshot([Notice(1), Notice(2)], Now)); Assert.Empty(feed.Recent);
    }
    [Fact] public void StoredTextIsBoundedAndCreatedTimeNormalizedToUtc()
    {
        var feed = new NotificationFeed(); feed.ApplySnapshot([], Now);
        feed.ApplySnapshot([Notice(1) with { SourceName = new('s', 400), Title = new('t', 1000), Body = new('b', 9000), CreatedUtc = Now.ToOffset(TimeSpan.FromHours(7)) }], Now);
        Assert.Equal(256, feed.Current!.SourceName.Length); Assert.Equal(512, feed.Current.Title.Length);
        Assert.Equal(2048, feed.Current.Body.Length); Assert.Equal(TimeSpan.Zero, feed.Current.CreatedUtc.Offset);
    }
}
