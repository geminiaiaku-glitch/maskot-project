using Maskot.Core;

namespace Maskot.Core.Tests;

public sealed class ProductivityTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 0, 0, 0, TimeSpan.Zero);
    [Fact] public void TimerPauseResumeDoesNotLoseElapsedTime()
    {
        var t = new FocusTimer(); t.Start(Now); t.Pause(Now.AddSeconds(45));
        Assert.Equal(1455, t.Remaining(Now.AddHours(1))); t.Start(Now.AddHours(2));
        Assert.Equal(1425, t.Remaining(Now.AddHours(2).AddSeconds(30)));
    }
    [Fact] public void SleepCompletesOnlyOnePhaseAndPreparesNextPaused()
    {
        var t = new FocusTimer(); t.Start(Now); Assert.True(t.Advance(Now.AddHours(12)));
        Assert.False(t.Running); Assert.Equal(FocusPhase.Break, t.Phase); Assert.Equal(300, t.Remaining(Now));
        Assert.False(t.Advance(Now.AddHours(13)));
    }
    [Fact] public void DeadlineBoundaryAndReset()
    {
        var t = new FocusTimer(); t.Start(Now); Assert.False(t.Advance(Now.AddSeconds(1499)));
        Assert.True(t.Advance(Now.AddSeconds(1500))); t.Start(Now.AddSeconds(1500));
        Assert.True(t.Advance(Now.AddSeconds(1800))); Assert.Equal(FocusPhase.Focus, t.Phase);
        t.Reset(); Assert.Equal(1500, t.Remaining(Now));
    }
    [Fact] public void RepeatedStartDoesNotExtendTimer()
    {
        var t = new FocusTimer(); t.Start(Now); t.Start(Now.AddSeconds(100)); Assert.Equal(1400, t.Remaining(Now.AddSeconds(100)));
    }
    [Fact] public void LrcHandlesMultipleTagsOffsetsAndNonTimestampMetadata()
    {
        var l = LrcParser.Parse("[ar:Artist]\n[offset:-100]\n[00:10.20][00:30.5]hello\n[00:20.00]world");
        Assert.Equal(3, l.Count); Assert.Equal(TimeSpan.FromSeconds(10.1), l[0].At);
        Assert.Equal("", LrcParser.Current(l, TimeSpan.Zero)); Assert.Equal("world", LrcParser.Current(l, TimeSpan.FromSeconds(25)));
        Assert.Equal("hello", LrcParser.Current(l, TimeSpan.FromMinutes(1)));
    }
    [Fact] public void LrcIgnoresMalformedContent()
    {
        Assert.Empty(LrcParser.Parse("[00:xx]bad\nplain lyrics\n[999999999999:10]bad"));
    }
    [Fact] public void PersistenceRoundTripsFeaturesAndRunningDeadline()
    {
        WithStore((store, path) =>
        {
            var s = new AppState { Notes = "catatan\n🐾", Placement = new(MascotMode.Notch, new(-100, 0), "second") };
            s.Tasks.Add(new() { Text = "test", Done = true }); s.Shelf.Add(new() { Path = Path.Combine(path, "missing.txt") });
            s.Timer.Start(Now); store.Save(s); var read = store.Load();
            Assert.Equal(s.Notes, read.Notes); Assert.Equal(s.Placement, read.Placement);
            Assert.True(read.Tasks.Single().Done); Assert.False(read.Shelf.Single().Exists);
            Assert.Equal(s.Timer.DeadlineUtc, read.Timer.DeadlineUtc); Assert.False(File.Exists(store.FilePath + ".tmp"));
        });
    }
    [Fact] public void CorruptPrimaryRecoversBackupWithoutOverwritingIt()
    {
        WithStore((store, _) =>
        {
            var s = new AppState { Notes = "safe" }; store.Save(s); s.Notes = "newer"; store.Save(s);
            File.WriteAllText(store.FilePath, "{broken"); var read = store.Load(); Assert.Equal("safe", read.Notes);
            Assert.NotNull(store.RecoveryNotice); store.Save(read); Assert.Equal("safe", store.Load().Notes);
            Assert.Contains("safe", File.ReadAllText(store.FilePath + ".bak"));
            Assert.Single(Directory.GetFiles(Path.GetDirectoryName(store.FilePath)!, "*.corrupt-*"));
        });
    }
    [Fact] public void CorruptPrimaryWithoutBackupStartsSafely()
    {
        WithStore((store, _) => { File.WriteAllText(store.FilePath, "not json"); Assert.Equal("", store.Load().Notes); Assert.NotNull(store.RecoveryNotice); });
    }
    [Fact] public void NormalizationClampsUnsafeSettings()
    {
        var s = new AppState(); s.Preferences.Size = double.NaN; s.Preferences.WalkSpeed = 1000;
        s.Timer.FocusMinutes = -3; s.Timer.RemainingSeconds = double.NaN;
        s.Placement = new(MascotMode.Stationary, new(double.PositiveInfinity, 1), "main"); s.Normalize();
        Assert.Equal(1, s.Preferences.Size); Assert.Equal(100, s.Preferences.WalkSpeed); Assert.Equal(1, s.Timer.FocusMinutes);
        Assert.Equal(60, s.Timer.RemainingSeconds); Assert.Null(s.Placement);
    }
    [Fact] public void ShelfOnlyReferencesOriginalFile()
    {
        WithStore((store, dir) =>
        {
            var original = Path.Combine(dir, "original.txt"); File.WriteAllText(original, "keep me");
            var state = new AppState(); state.Shelf.Add(new() { Path = original }); store.Save(state);
            state.Shelf.Clear(); store.Save(state); Assert.Equal("keep me", File.ReadAllText(original));
        });
    }
    [Fact] public void MissingPrimaryRecoversExistingBackup()
    {
        WithStore((store, _) =>
        {
            store.Save(new() { Notes = "backup notes", Tasks = [new() { Text = "keep task" }] });
            store.Save(new() { Notes = "later" });
            File.Delete(store.FilePath);
            var recovered = store.Load();
            Assert.Equal("backup notes", recovered.Notes); Assert.Equal("keep task", recovered.Tasks.Single().Text);
            Assert.NotNull(store.RecoveryNotice);
            store.Save(recovered); Assert.Equal("backup notes", store.Load().Notes);
        });
    }
    [Fact] public void RepeatedAutosaveKeepsPreviousDistinctBackup()
    {
        WithStore((store, _) =>
        {
            var state = new AppState { Notes = "previous" }; store.Save(state);
            state.Notes = "current"; store.Save(state);
            var actualWrite = File.GetLastWriteTimeUtc(store.FilePath);
            store.Save(state);
            Assert.Equal(actualWrite, File.GetLastWriteTimeUtc(store.FilePath));
            Assert.Contains("previous", File.ReadAllText(store.FilePath + ".bak"));
            File.Delete(store.FilePath); store.Save(state);
            Assert.Equal("current", store.Load().Notes);
        });
    }
    [Fact] public void UnchangedAutosaveRepairsExternallyCorruptedPrimary()
    {
        WithStore((store, path) =>
        {
            var state = new AppState { Notes = "safe" }; store.Save(state);
            File.WriteAllText(store.FilePath, "{broken");
            store.Save(state);
            Assert.Equal("safe", store.Load().Notes);
            Assert.Single(Directory.GetFiles(path, "*.corrupt-*"));
        });
    }
    [Fact] public void RestartAutosavePreservesPreviousDistinctBackup()
    {
        WithStore((store, path) =>
        {
            var state = new AppState { Notes = "previous" }; state.Normalize(); store.Save(state);
            state.Notes = "current"; store.Save(state);
            var restarted = new StateStore(path); var loaded = restarted.Load(); restarted.Save(loaded);
            Assert.Contains("previous", File.ReadAllText(store.FilePath + ".bak"));
        });
    }
    [Fact] public void NullCollectionEntriesAndFieldsDoNotDiscardValidData()
    {
        WithStore((store, _) =>
        {
            File.WriteAllText(store.FilePath, """
                {"Notes":"keep notes","Tasks":[null,{"Text":"keep task"},{"Text":null}],
                 "Shelf":[null,{"Path":"keep original.txt"},{"Path":null}]}
                """);
            var state = store.Load();
            Assert.Null(store.RecoveryNotice); Assert.Equal("keep notes", state.Notes);
            Assert.Equal(new[] { "keep task", "" }, state.Tasks.Select(t => t.Text));
            Assert.Equal(new[] { "keep original.txt", "" }, state.Shelf.Select(f => f.Path));
            store.Save(state);
            using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(store.FilePath));
            var shelf = json.RootElement.GetProperty("Shelf")[0];
            Assert.False(shelf.TryGetProperty("Exists", out var exists)); Assert.False(shelf.TryGetProperty("Name", out var name));
            Assert.Equal(2, store.Load().Shelf.Count);
        });
    }
    [Fact] public void InvalidTimerStateCannotOverflowUiOrStartDeadline()
    {
        var state = new AppState(); state.Timer.Phase = (FocusPhase)123;
        state.Timer.DeadlineUtc = Now.AddDays(1); state.Timer.RemainingSeconds = double.MaxValue;
        state.Normalize();
        Assert.Equal(FocusPhase.Focus, state.Timer.Phase); Assert.False(state.Timer.Running);
        Assert.Equal(1500, state.Timer.RemainingSeconds);
        state.Timer.RemainingSeconds = 1e20; state.Normalize(); state.Timer.Start(Now);
        Assert.Equal(Now.AddMinutes(25), state.Timer.DeadlineUtc);
    }
    private static void WithStore(Action<StateStore, string> action)
    {
        var dir = Path.Combine(Path.GetTempPath(), "maskot-tests-" + Guid.NewGuid()); Directory.CreateDirectory(dir);
        try { action(new(dir), dir); } finally { Directory.Delete(dir, true); }
    }
}
