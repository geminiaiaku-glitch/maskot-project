namespace Maskot.Core;

public enum PeekAction { None, Open, Close }

/// <summary>Hover intent and leave grace without stealing focus or reopening an explicitly dismissed panel.</summary>
public sealed class NotchPeek
{
    private TimeSpan? _entered, _left;
    private bool _dismissed;
    public bool Pending => _entered.HasValue;

    public PeekAction Update(TimeSpan now, bool enabled, bool inside, bool panelOpen, bool preview, bool editing)
    {
        if (!enabled) { _entered = _left = null; return PeekAction.None; }
        if (!inside) _dismissed = false;
        if (panelOpen)
        {
            _entered = null;
            if (!preview || inside || editing) { _left = null; return PeekAction.None; }
            _left ??= now;
            if (now - _left >= TimeSpan.FromMilliseconds(400)) { _left = null; return PeekAction.Close; }
        }
        else
        {
            _left = null;
            if (!inside || _dismissed) { _entered = null; return PeekAction.None; }
            _entered ??= now;
            if (now - _entered >= TimeSpan.FromMilliseconds(180)) { _entered = null; return PeekAction.Open; }
        }
        return PeekAction.None;
    }
    public void Dismiss() { _dismissed = true; _entered = _left = null; }
}
