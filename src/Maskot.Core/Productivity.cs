using System.Text.Json.Serialization;

namespace Maskot.Core;

public enum FocusPhase { Focus, Break }
public sealed class FocusTimer
{
    public int FocusMinutes { get; set; } = 25;
    public int BreakMinutes { get; set; } = 5;
    public FocusPhase Phase { get; set; }
    public DateTimeOffset? DeadlineUtc { get; set; }
    public double RemainingSeconds { get; set; } = 25 * 60;
    public bool Running => DeadlineUtc.HasValue;
    public double Remaining(DateTimeOffset now) => Math.Max(0, DeadlineUtc is { } end ? (end - now).TotalSeconds : RemainingSeconds);
    public void Start(DateTimeOffset now)
    {
        if (!Running) DeadlineUtc = now.AddSeconds(Math.Max(1, RemainingSeconds));
    }
    public void Pause(DateTimeOffset now)
    {
        if (!Running) return;
        RemainingSeconds = Remaining(now);
        DeadlineUtc = null;
    }
    public void Reset()
    {
        DeadlineUtc = null;
        RemainingSeconds = Math.Clamp(Phase == FocusPhase.Focus ? FocusMinutes : BreakMinutes, 1, 180) * 60;
    }
    public bool Advance(DateTimeOffset now)
    {
        if (DeadlineUtc is null || now < DeadlineUtc) return false;
        // Elapsed real time includes sleep. Complete once, prepare the next phase paused.
        Phase = Phase == FocusPhase.Focus ? FocusPhase.Break : FocusPhase.Focus;
        Reset();
        return true;
    }
}

public sealed class TaskItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Text { get; set; } = "";
    public bool Done { get; set; }
}

public sealed class ShelfItem
{
    public string Path { get; set; } = "";
    [JsonIgnore]
    public string Name => System.IO.Path.GetFileName(Path.TrimEnd(System.IO.Path.DirectorySeparatorChar));
    [JsonIgnore]
    public bool Exists => File.Exists(Path) || Directory.Exists(Path);
}

public sealed class Preferences
{
    // Zero identifies settings written before the animated/always-visible release.
    public int BehaviorVersion { get; set; }
    public double Size { get; set; } = 1;
    public double WalkSpeed { get; set; } = 32;
    public bool ExtraAnimations { get; set; } = true;
    public bool HideInFullscreen { get; set; }
    public bool LyricsEnabled { get; set; }
    public bool StartAtLogin { get; set; }
}

public sealed class AppState
{
    public int SchemaVersion { get; set; } = 1;
    public MascotPlacement? Placement { get; set; }
    public Preferences Preferences { get; set; } = new();
    public FocusTimer Timer { get; set; } = new();
    public string Notes { get; set; } = "";
    public List<TaskItem> Tasks { get; set; } = [];
    public List<ShelfItem> Shelf { get; set; } = [];

    public void Normalize()
    {
        Preferences ??= new(); Timer ??= new(); Tasks ??= []; Shelf ??= []; Notes ??= "";
        Tasks.RemoveAll(task => task is null);
        Shelf.RemoveAll(item => item is null);
        foreach (var task in Tasks) task.Text ??= "";
        foreach (var item in Shelf) item.Path ??= "";
        if (Preferences.BehaviorVersion < 2)
        {
            Preferences.HideInFullscreen = false;
            Preferences.ExtraAnimations = true;
            Preferences.BehaviorVersion = 2;
        }
        Preferences.Size = double.IsFinite(Preferences.Size) ? Math.Clamp(Preferences.Size, 0.65, 1.8) : 1;
        Preferences.WalkSpeed = double.IsFinite(Preferences.WalkSpeed) ? Math.Clamp(Preferences.WalkSpeed, 10, 100) : 32;
        Timer.FocusMinutes = Math.Clamp(Timer.FocusMinutes, 1, 180);
        Timer.BreakMinutes = Math.Clamp(Timer.BreakMinutes, 1, 180);
        if (!Enum.IsDefined(Timer.Phase)) { Timer.Phase = FocusPhase.Focus; Timer.Reset(); }
        if (!double.IsFinite(Timer.RemainingSeconds) || Timer.RemainingSeconds < 0 || Timer.RemainingSeconds > 180 * 60) Timer.Reset();
        if (Placement is { } p && (!double.IsFinite(p.Position.X) || !double.IsFinite(p.Position.Y))) Placement = null;
    }
}
