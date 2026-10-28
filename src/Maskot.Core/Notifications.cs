namespace Maskot.Core;

public sealed record DesktopNotice(uint Id, string SourceId, string SourceName, string Title, string Body, DateTimeOffset CreatedUtc);

/// <summary>Session-only presentation state. It never removes or writes Windows notifications.</summary>
public sealed class NotificationFeed(string? ownSourceId = null)
{
    public const int HistoryLimit = 30;
    public const int PendingLimit = 10;
    public static readonly TimeSpan DisplayTime = TimeSpan.FromSeconds(8);
    private const int IdentityLimit = 4096;
    private readonly record struct Identity(uint Id, string Source, DateTimeOffset Created);
    private readonly List<DesktopNotice> _recent = [];
    private IReadOnlyList<DesktopNotice>? _readOnlyRecent;
    private readonly Queue<DesktopNotice> _pending = new();
    private readonly HashSet<Identity> _seen = [];
    private HashSet<Identity> _snapshot = [];
    private readonly Queue<Identity> _seenOrder = new();
    private bool _baseline = true;
    private bool _suspended;
    private DateTimeOffset _expiresAt;
    public IReadOnlyList<DesktopNotice> Recent => _readOnlyRecent ??= _recent.AsReadOnly();
    public DesktopNotice? Current { get; private set; }
    public int PendingCount => _pending.Count;
    public bool IsSuspended => _suspended;

    public bool ApplySnapshot(IEnumerable<DesktopNotice> notices, DateTimeOffset now)
    {
        var changed = false;
        var snapshot = new HashSet<Identity>();
        foreach (var item in notices.OrderBy(n => n.CreatedUtc).ThenBy(n => n.Id))
        {
            if (!string.IsNullOrEmpty(ownSourceId) && string.Equals(item.SourceId, ownSourceId, StringComparison.OrdinalIgnoreCase)) continue;
            var identity = new Identity(item.Id, item.SourceId, item.CreatedUtc.ToUniversalTime());
            snapshot.Add(identity);
            if (_snapshot.Contains(identity) || !_seen.Add(identity)) continue;
            _seenOrder.Enqueue(identity);
            while (_seenOrder.Count > IdentityLimit) _seen.Remove(_seenOrder.Dequeue());
            if (_baseline || _suspended) continue;
            // Bound text as well as item counts, including data from custom Windows toast templates.
            var notice = item with { SourceName = Limit(item.SourceName, 256), Title = Limit(item.Title, 512),
                Body = Limit(item.Body, 2048), CreatedUtc = identity.Created };
            _recent.Insert(0, notice);
            if (_recent.Count > HistoryLimit) _recent.RemoveAt(_recent.Count - 1);
            if (_pending.Count == PendingLimit) _pending.Dequeue();
            _pending.Enqueue(notice); changed = true;
        }
        _snapshot = snapshot; _baseline = false;
        return StartNext(now) || changed;
    }
    private static string Limit(string? value, int length) => value is null ? "" : value.Length <= length ? value : value[..length];
    private bool StartNext(DateTimeOffset now)
    {
        if (_suspended || Current is not null || !_pending.TryDequeue(out var next)) return false;
        Current = next; _expiresAt = now + DisplayTime; return true;
    }
    public bool Advance(DateTimeOffset now, bool hold)
    {
        if (_suspended) return false;
        if (Current is null) return StartNext(now);
        if (hold) { _expiresAt = now + DisplayTime; return false; }
        if (now < _expiresAt) return false;
        Current = null; StartNext(now); return true;
    }
    public bool DismissCurrent(DateTimeOffset now)
    {
        if (Current is null) return false;
        Current = null; StartNext(now); return true;
    }
    public bool SuspendPresentation(bool suspended)
    {
        if (_suspended == suspended) return false;
        _suspended = suspended;
        // Drop queued cards on both edges. The first fresh snapshot after resume is a baseline,
        // including notifications arriving between the last quiet/lock poll and the resume event.
        Current = null; _pending.Clear(); _baseline = true;
        return true;
    }
    public bool Clear()
    {
        var changed = Current is not null || _pending.Count > 0 || _recent.Count > 0;
        Current = null; _pending.Clear(); _recent.Clear();
        // Retain only identities: clearing the local history must not replay Windows' current list.
        return changed;
    }
    public void Reset()
    {
        Clear(); _seen.Clear(); _seenOrder.Clear(); _snapshot.Clear(); _baseline = true;
    }
}
