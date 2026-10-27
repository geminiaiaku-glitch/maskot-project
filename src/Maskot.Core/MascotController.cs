namespace Maskot.Core;

public enum MascotMode { Walking, Stationary, Notch }
public enum MascotPose { Idle, Walk, Lifted, Sit, Sleep, Greet }
public sealed record MascotPlacement(MascotMode Mode, PointD Position, string? MonitorId);

public sealed class MascotController
{
    private PointD _press, _offset;
    private MascotPlacement? _beforeDrag;
    private double _moodSeconds;
    private double _resumeDelay;
    private bool _undocked;
    private bool _dockArmed;
    private readonly Random _random;
    public MascotMode Mode { get; private set; } = MascotMode.Walking;
    public PointD Position { get; private set; }
    public string? MonitorId { get; private set; }
    public MascotPose Pose { get; private set; }
    public bool PointerHeld { get; private set; }
    public bool Dragging { get; private set; }
    public bool PanelOpen { get; private set; }
    public bool FacingRight { get; private set; } = true;
    public bool DockEligible { get; private set; }
    public bool DockNearby { get; private set; }
    public DisplayArea? DragDisplay { get; private set; }
    public double Size { get; set; } = 1;
    public double Speed { get; set; } = 32;
    public bool ExtraAnimations { get; set; } = true;
    public double NotchWidth { get; private set; } = 230;
    public double NotchHeight { get; private set; } = 42;

    public MascotController(int? seed = null) => _random = seed is int s ? new(s) : new();
    public (double Width, double Height) Dimensions(double scale) =>
        Mode == MascotMode.Notch ? (NotchWidth * scale, NotchHeight * scale) : (112 * Size * scale, 142 * Size * scale);
    public MascotPlacement Snapshot() => new(Mode, Position, MonitorId);

    public void Restore(MascotPlacement? placement, IReadOnlyList<DisplayArea> displays)
    {
        var d = displays.FirstOrDefault(d => d.Id == placement?.MonitorId)
                ?? DesktopGeometry.Nearest(displays, placement?.Position ?? displays[0].Bounds.Center);
        MonitorId = d.Id;
        Mode = placement?.Mode ?? MascotMode.Walking;
        if (!Enum.IsDefined(Mode)) Mode = MascotMode.Walking;
        var (w, h) = Dimensions(d.Scale);
        Position = Mode == MascotMode.Notch ? DockPosition(d) :
            d.WorkArea.Clamp(placement?.Position ?? new(d.Bounds.Center.X - w / 2, d.WalkingBottom - h), w, h);
        if (Mode == MascotMode.Walking) Position = new(Position.X, d.WalkingBottom - h);
        Pose = Mode == MascotMode.Walking ? MascotPose.Walk : MascotPose.Idle;
        _moodSeconds = 8;
    }

    private PointD DockPosition(DisplayArea d) => new(d.Bounds.Center.X - NotchWidth * d.Scale / 2, d.Bounds.Y);
    public void SetLyricNotch(bool expanded, IReadOnlyList<DisplayArea> displays)
    {
        if (PointerHeld) return;
        var display = displays.FirstOrDefault(d => d.Id == MonitorId) ?? DesktopGeometry.Nearest(displays, Position);
        var width = expanded ? Math.Min(360, display.Bounds.Width / display.Scale - 16) : 230;
        var height = expanded ? 44 : 42;
        if (NotchWidth == width && NotchHeight == height) return;
        NotchWidth = width; NotchHeight = height;
        if (Mode == MascotMode.Notch) Position = DockPosition(display);
    }

    public void ReconcileDisplays(IReadOnlyList<DisplayArea> displays)
    {
        if (PointerHeld) return;
        var d = displays.FirstOrDefault(d => d.Id == MonitorId) ?? DesktopGeometry.Nearest(displays, Position);
        MonitorId = d.Id;
        var (w, h) = Dimensions(d.Scale);
        Position = Mode == MascotMode.Notch ? DockPosition(d) : d.Bounds.Clamp(Position, w, h);
    }

    public void SetPanel(bool open)
    {
        if (PanelOpen == open) return;
        PanelOpen = open;
        if (!open) { _resumeDelay = 0.6; _moodSeconds = 8; }
        Pose = !open && Mode == MascotMode.Walking ? MascotPose.Walk : MascotPose.Idle;
    }

    public void Walk(DisplayArea d)
    {
        Mode = MascotMode.Walking;
        MonitorId = d.Id;
        var (w, h) = Dimensions(d.Scale);
        Position = d.WorkArea.Clamp(new(Position.X, d.WalkingBottom - h), w, h);
        _moodSeconds = 8;
        _resumeDelay = 0;
        Pose = MascotPose.Walk;
    }

    public void Stay() { if (Mode != MascotMode.Notch) Mode = MascotMode.Stationary; Pose = MascotPose.Idle; }

    public void Press(PointD cursor)
    {
        _press = cursor;
        _offset = cursor - Position;
        _beforeDrag = Snapshot();
        PointerHeld = true;
        Dragging = false;
        _undocked = false;
        _dockArmed = true;
    }

    public void MovePointer(PointD cursor, IReadOnlyList<DisplayArea> displays)
    {
        if (!PointerHeld) return;
        var d = DesktopGeometry.Nearest(displays, cursor);
        if (!Dragging && (cursor - _press).Length < 6 * d.Scale) return;
        Dragging = true;
        PanelOpen = false;
        if (Mode == MascotMode.Notch)
        {
            // A small wiggle on the notch never turns into a click or an accidental undock.
            if (cursor.Y - _press.Y < 62 * d.Scale) return;
            Mode = MascotMode.Stationary;
            _undocked = true;
            _dockArmed = false;
            _offset = new(56 * Size * d.Scale, 36 * Size * d.Scale);
        }
        Pose = MascotPose.Lifted;
        MonitorId = d.Id;
        DragDisplay = d;
        var (w, h) = Dimensions(d.Scale);
        Position = d.Bounds.Clamp(cursor - _offset, w, h);
        // Eligibility uses the pointer, with a larger exit rectangle for stable hysteresis.
        var t = d.DockTarget;
        if (!t.Contains(cursor)) _dockArmed = true;
        var exit = new RectD(t.X - 14 * d.Scale, t.Y, t.Width + 28 * d.Scale, t.Height + 14 * d.Scale);
        DockEligible = _dockArmed && (DockEligible ? exit : t).Contains(cursor);
        DockNearby = new RectD(t.X - 110 * d.Scale, t.Y, t.Width + 220 * d.Scale, 220 * d.Scale).Contains(cursor);
    }

    // Returns true only for a click. A completed drag can never toggle the panel.
    public bool Release(IReadOnlyList<DisplayArea> displays)
    {
        if (!PointerHeld) return false;
        var click = !Dragging;
        if (Dragging)
        {
            if (Mode != MascotMode.Notch || _undocked)
            {
                var d = DragDisplay ?? DesktopGeometry.Nearest(displays, Position);
                Mode = DockEligible ? MascotMode.Notch : MascotMode.Stationary;
                if (Mode == MascotMode.Notch) Position = DockPosition(d);
            }
            Pose = MascotPose.Idle;
        }
        EndPointer();
        return click;
    }

    public void CancelDrag()
    {
        if (!PointerHeld) return;
        if (_beforeDrag is { } p) { Mode = p.Mode; Position = p.Position; MonitorId = p.MonitorId; }
        Pose = MascotPose.Idle;
        EndPointer();
    }

    private void EndPointer()
    {
        PointerHeld = false; Dragging = false; DockEligible = false; DockNearby = false;
        DragDisplay = null; _beforeDrag = null;
    }

    public void Tick(double seconds, PointD cursor, IReadOnlyList<DisplayArea> displays)
    {
        if (PointerHeld || PanelOpen) return;
        seconds = Math.Clamp(seconds, 0, 0.1); // Never teleport after a stalled UI tick / sleep.
        if (_resumeDelay > 0) { _resumeDelay -= seconds; return; }
        var d = displays.FirstOrDefault(d => d.Id == MonitorId) ?? DesktopGeometry.Nearest(displays, Position);
        if (Mode != MascotMode.Walking) { Pose = MascotPose.Idle; return; }
        var (w, h) = Dimensions(d.Scale);
        var bottom = d.WalkingBottom - h;
        Position = new(Position.X, Position.Y + (bottom - Position.Y) * Math.Min(1, seconds * 8));
        var bounds = new RectD(Position.X - 35 * d.Scale, Position.Y - 30 * d.Scale, w + 70 * d.Scale, h + 60 * d.Scale);
        if (bounds.Contains(cursor)) { Pose = MascotPose.Greet; return; }
        if (Pose == MascotPose.Greet) _moodSeconds = 0;
        _moodSeconds -= seconds;
        if (_moodSeconds <= 0)
        {
            Pose = ExtraAnimations ? _random.Next(10) switch
            { 0 => MascotPose.Sleep, 1 or 2 => MascotPose.Sit, 3 => MascotPose.Idle, _ => MascotPose.Walk } : MascotPose.Walk;
            _moodSeconds = Pose == MascotPose.Walk ? _random.Next(8, 18) : _random.Next(3, 7);
        }
        if (Pose != MascotPose.Walk) return;
        var x = Position.X + (FacingRight ? 1 : -1) * Speed * d.Scale * seconds;
        var left = d.WorkArea.X + 4 * d.Scale;
        var right = Math.Max(left, d.WorkArea.Right - w - 4 * d.Scale);
        if (x <= left) { x = left; FacingRight = true; }
        if (x >= right) { x = right; FacingRight = false; }
        Position = new(x, Position.Y);
    }
}
